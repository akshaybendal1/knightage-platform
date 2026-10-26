using Knightage.Platform.Api.Contracts;
using Knightage.Platform.Core;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly IProvisioningService _provisioningService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IPlanRepository _planRepository;
    private readonly ITenantMigrationRepository _migrationRepository;
    private readonly ICrmCacheInvalidator _crmCacheInvalidator;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        IProvisioningService provisioningService,
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        ISubscriptionRepository subscriptionRepository,
        IPlanRepository planRepository,
        ITenantMigrationRepository migrationRepository,
        ICrmCacheInvalidator crmCacheInvalidator,
        IConfiguration configuration,
        IEmailSender emailSender,
        ILogger<TenantsController> logger)
    {
        _provisioningService = provisioningService;
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _migrationRepository = migrationRepository;
        _crmCacheInvalidator = crmCacheInvalidator;
        _configuration = configuration;
        _emailSender = emailSender;
        _logger = logger;
    }

    // The tenant-facing "pick a service" launcher: any authenticated user, scoped to their
    // own org_id claim (never a caller-supplied id -- there is nothing here a PlatformAdmin
    // needs that GetByOrganizationId doesn't already give them).
    [HttpGet("me/services")]
    public async Task<IActionResult> GetMyServices()
    {
        var callerOrgIdClaim = User.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(callerOrgIdClaim, out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        var databases = tenant is null
            ? Array.Empty<Core.Models.TenantServiceDatabase>()
            : (await _databaseRepository.GetByTenantIdAsync(tenant.Id)).ToArray();

        var services = ServiceCatalog.Entries.Select(entry => new
        {
            key = entry.Key,
            displayName = entry.DisplayName,
            url = _configuration[$"Client:ServiceUrls:{entry.Key}"],
            provisioned = databases.Any(d =>
                string.Equals(d.ServiceName, entry.Key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(d.Status, "Provisioned", StringComparison.OrdinalIgnoreCase)),
        });

        var plan = tenant?.PlanId is Guid planId ? await _planRepository.GetByIdAsync(planId) : null;
        // Every active plan, so the tenant can see what an upgrade/downgrade would look
        // like -- same PlatformAdmin-assigned-only model as everything else here, so this
        // is a picker for the *request* email below, not a self-serve switch.
        var availablePlans = await _planRepository.GetAllAsync(includeInactive: false);

        return Ok(new
        {
            services,
            // Surfaces the sub-feature toggle (set via /tenants by a PlatformAdmin, see
            // UpdateFieldForceTracking) on the tenant's own services page -- previously only
            // visible to a PlatformAdmin, not to the org whose CRM it actually affects.
            fieldForceTrackingEnabled = tenant?.FieldForceTrackingEnabled ?? false,
            plan = BuildPlanSummary(plan),
            availablePlans = availablePlans.Select(BuildPlanSummary),
        });
    }

    // Lets a tenant ask for something a PlatformAdmin needs to act on by hand -- either a
    // not-yet-provisioned service (this endpoint) or the Field Force Tracking sub-feature
    // (RequestFieldForceTracking below). Both just email the admin the same way
    // knightage-crm's OrganizationController.RequestSubscription already does; no request
    // state is persisted, so repeat clicks just send another email.
    [HttpPost("me/services/{key}/request")]
    public async Task<IActionResult> RequestService(string key, CancellationToken cancellationToken)
    {
        var entry = ServiceCatalog.Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return BadRequest(new { message = $"'{key}' is not a known service." });
        }

        var callerOrgIdClaim = User.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(callerOrgIdClaim, out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        return await SendAdminRequestEmailAsync(
            subject: $"Service access request: {tenant?.Name ?? "(unknown org)"} wants {entry.DisplayName}",
            body: $"""
                {RequesterLine()} requested access to {entry.DisplayName}.

                Organization: {tenant?.Name ?? "(unknown org)"}
                Organization ID: {organizationId}
                Requested: {DateTime.UtcNow:u}

                Provision it from the Tenants page: https://platform.knightagetech.com/tenants
                """,
            cancellationToken);
    }

    [HttpPost("me/field-force-tracking/request")]
    public async Task<IActionResult> RequestFieldForceTracking(CancellationToken cancellationToken)
    {
        var callerOrgIdClaim = User.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(callerOrgIdClaim, out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        return await SendAdminRequestEmailAsync(
            subject: $"Field Force Tracking request: {tenant?.Name ?? "(unknown org)"}",
            body: $"""
                {RequesterLine()} requested Field Force Tracking be enabled for their CRM.

                Organization: {tenant?.Name ?? "(unknown org)"}
                Organization ID: {organizationId}
                Requested: {DateTime.UtcNow:u}

                Enable it from the Tenants page: https://platform.knightagetech.com/tenants
                """,
            cancellationToken);
    }

    [HttpPost("me/plan/request")]
    public async Task<IActionResult> RequestPlanChange(RequestPlanChangeRequest request, CancellationToken cancellationToken)
    {
        var callerOrgIdClaim = User.FindFirst("org_id")?.Value;
        if (!Guid.TryParse(callerOrgIdClaim, out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var requestedPlan = await _planRepository.GetByIdAsync(request.PlanId);
        if (requestedPlan is null)
        {
            return BadRequest(new { message = "No plan with this id." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        var currentPlan = tenant?.PlanId is Guid currentPlanId ? await _planRepository.GetByIdAsync(currentPlanId) : null;

        return await SendAdminRequestEmailAsync(
            subject: $"Plan change request: {tenant?.Name ?? "(unknown org)"} wants {requestedPlan.Name}",
            body: $"""
                {RequesterLine()} requested to move to the {requestedPlan.Name} plan.

                Organization: {tenant?.Name ?? "(unknown org)"}
                Organization ID: {organizationId}
                Current plan: {currentPlan?.Name ?? "None (unlimited)"}
                Requested plan: {requestedPlan.Name} ({requestedPlan.MaxUsers?.ToString() ?? "unlimited"} users / {requestedPlan.MaxRecords?.ToString() ?? "unlimited"} records / {(requestedPlan.PriceMonthly is decimal price ? $"₹{price}/mo" : "custom pricing")})
                Requested: {DateTime.UtcNow:u}

                Assign it from the Tenants page: https://platform.knightagetech.com/tenants
                """,
            cancellationToken);
    }

    private string RequesterLine()
    {
        var requesterName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown user";
        var requesterEmail = User.FindFirst("email")?.Value ?? "unknown";
        return $"{requesterName} ({requesterEmail})";
    }

    private async Task<IActionResult> SendAdminRequestEmailAsync(string subject, string body, CancellationToken cancellationToken)
    {
        var platformAdminEmail = _configuration["App:PlatformAdminEmail"];
        if (string.IsNullOrWhiteSpace(platformAdminEmail))
        {
            _logger.LogError("App:PlatformAdminEmail is not configured; cannot send request email.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Requests aren't available right now. Try again later." });
        }

        try
        {
            await _emailSender.SendAsync(platformAdminEmail, subject, body, cancellationToken);
            return Ok(new { message = "Request sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send request email.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Couldn't send the request. Try again in a moment." });
        }
    }

    [HttpPost("provision")]
    public async Task<IActionResult> Provision(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _provisioningService.ProvisionAsync(request.OrganizationId, request.OrganizationName, cancellationToken);
        return Ok(new { tenant = result.Tenant, databases = result.Databases });
    }

    // Lists every tenant across every organization, with its subscription status, so a
    // PlatformAdmin can see every vendor's state at a glance rather than clicking into each
    // one -- PlatformAdmin only. A regular user's own tenant info comes from
    // GetByOrganizationId below, which every business app's tenant-resolution middleware
    // already depends on and stays open to any authenticated user.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery(Name = "plan")] string? planFilter = null,
        [FromQuery] string? subscriptionStatus = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // A filter value that isn't recognised is rejected rather than ignored: silently
        // dropping it would show every tenant while the admin believes the list is narrowed.
        // Sort is only presentation, so an unknown key just falls back to the default order.
        if (!string.IsNullOrWhiteSpace(planFilter) && planFilter != Core.Models.TenantListQuery.PlanNone && !Guid.TryParse(planFilter, out _))
        {
            return BadRequest(new { message = "plan must be a plan id or 'none'." });
        }

        string[] subscriptionStatuses =
        [
            SubscriptionStatusCalculator.Active,
            SubscriptionStatusCalculator.ReadOnly,
            SubscriptionStatusCalculator.Disabled,
            SubscriptionStatusCalculator.NotSubscribed,
        ];
        if (!string.IsNullOrWhiteSpace(subscriptionStatus) && !subscriptionStatuses.Contains(subscriptionStatus))
        {
            return BadRequest(new { message = $"subscriptionStatus must be one of: {string.Join(", ", subscriptionStatuses)}." });
        }

        string[] sortKeys =
        [
            Core.Models.TenantListQuery.SortName,
            Core.Models.TenantListQuery.SortSlug,
            Core.Models.TenantListQuery.SortPlan,
            Core.Models.TenantListQuery.SortSubscription,
            Core.Models.TenantListQuery.SortEnds,
            Core.Models.TenantListQuery.SortCreated,
        ];
        var sortKey = sortKeys.FirstOrDefault(k => string.Equals(k, sortBy, StringComparison.OrdinalIgnoreCase)) ?? Core.Models.TenantListQuery.SortCreated;

        var query = new Core.Models.TenantListQuery
        {
            Search = search is { Length: > 100 } ? search[..100] : search,
            Plan = string.IsNullOrWhiteSpace(planFilter) ? null : planFilter,
            SubscriptionStatus = string.IsNullOrWhiteSpace(subscriptionStatus) ? null : subscriptionStatus,
            SortBy = sortKey,
            // Newest-first stays the default when no sort was asked for; once a sort is chosen,
            // ascending is the default unless the caller says otherwise.
            Descending = string.IsNullOrWhiteSpace(sortBy)
                ? !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase)
                : string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase),
        };

        var (tenants, totalCount) = await _tenantRepository.GetPagedAsync(page, pageSize, query);
        var rows = new List<object>(tenants.Count);
        foreach (var tenant in tenants)
        {
            var subscriptionRecord = await _subscriptionRepository.GetByTenantIdAsync(tenant.Id);
            var plan = tenant.PlanId is Guid planId ? await _planRepository.GetByIdAsync(planId) : null;
            rows.Add(new { tenant, subscription = BuildSubscriptionSummary(subscriptionRecord), plan = BuildPlanSummary(plan) });
        }

        return Ok(new { items = rows, totalCount });
    }

    // Open to any authenticated user, but only for their OWN organization -- every business
    // app's tenant-resolution middleware calls this with the caller's own org_id claim, so
    // that legitimate path is unaffected. Without this check, any authenticated user could
    // read any *other* organization's tenant/database info just by guessing/knowing its ID.
    [HttpGet("{organizationId:guid}")]
    public async Task<IActionResult> GetByOrganizationId(Guid organizationId)
    {
        if (!User.IsInRole("PlatformAdmin"))
        {
            var callerOrgIdClaim = User.FindFirst("org_id")?.Value;
            if (!Guid.TryParse(callerOrgIdClaim, out var callerOrganizationId) || callerOrganizationId != organizationId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only look up your own organization's tenant info." });
            }
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound();
        }

        var databases = await _databaseRepository.GetByTenantIdAsync(tenant.Id);
        var subscriptionRecord = await _subscriptionRepository.GetByTenantIdAsync(tenant.Id);
        var plan = tenant.PlanId is Guid planId ? await _planRepository.GetByIdAsync(planId) : null;
        var crmMigration = await _migrationRepository.GetLatestAsync(tenant.Id, "Crm");

        return Ok(new
        {
            tenant,
            databases,
            subscription = BuildSubscriptionSummary(subscriptionRecord),
            plan = BuildPlanSummary(plan),
            crmMigrationAwaitingPurge = crmMigration?.Status == Core.Models.TenantMigrationStatus.AwaitingPurge,
        });
    }

    // POST, not PUT -- the production host blocks PUT/DELETE at the server level (same
    // constraint as SubscriptionsController.UpdateSubscription).
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/accent-color")]
    public async Task<IActionResult> UpdateAccentColor(Guid organizationId, UpdateAccentColorRequest request)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        await _tenantRepository.UpdateAccentColorAsync(organizationId, request.AccentColor);
        return NoContent();
    }

    // POST, not PUT -- same production PUT/DELETE-blocking constraint as UpdateAccentColor above.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/field-force-tracking")]
    public async Task<IActionResult> UpdateFieldForceTracking(Guid organizationId, UpdateFieldForceTrackingRequest request)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        await _tenantRepository.UpdateFieldForceTrackingEnabledAsync(organizationId, request.Enabled);
        return NoContent();
    }

    // POST, not PUT -- same production PUT/DELETE-blocking constraint as UpdateAccentColor above.
    // Off (the default): the tenant's Accounting keeps the books of its own organization only, one
    // company. On: it can add more companies (an accounting firm keeping its clients' books).
    // knightage-accounting reads this from GetByOrganizationId (tenant.accountingMultiCompanyEnabled),
    // cached up to 15 minutes like the service switch.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/accounting-multi-company")]
    public async Task<IActionResult> UpdateAccountingMultiCompany(Guid organizationId, UpdateAccountingMultiCompanyRequest request)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        await _tenantRepository.UpdateAccountingMultiCompanyEnabledAsync(organizationId, request.Enabled);
        return NoContent();
    }

    // POST, not PUT -- same production PUT/DELETE-blocking constraint as the endpoints above.
    // This is the only thing that turns a service's "Provisioned" TenantServiceDatabases row
    // into "Disabled" (or back) -- CRM/InventorySales each check this via their own
    // TenantResolutionMiddleware (cached the same way subscription status already is), so
    // this is a real access control, not just hiding the tile on the tenant's own launcher.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/services/{key}/status")]
    public async Task<IActionResult> UpdateServiceStatus(Guid organizationId, string key, UpdateServiceStatusRequest request)
    {
        var entry = ServiceCatalog.Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return BadRequest(new { message = $"'{key}' is not a known service." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        // Enabling a service the tenant never had (older tenants were provisioned before
        // Accounting/InventorySales were added at sign-up) creates its row as "Provisioned".
        if (request.Enabled && await _provisioningService.AddServiceAsync(tenant, entry.Key))
        {
            return NoContent();
        }

        var status = request.Enabled ? "Provisioned" : "Disabled";
        var updated = await _databaseRepository.UpdateStatusAsync(tenant.Id, entry.Key, status);
        if (!updated)
        {
            return NotFound(new { message = $"{entry.DisplayName} hasn't been provisioned for this tenant yet." });
        }

        return NoContent();
    }

    // POST, not PUT -- same production PUT/DELETE-blocking constraint as the two endpoints
    // above. `PlanId: null` clears the tenant's plan back to unlimited/unrestricted.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/plan")]
    public async Task<IActionResult> UpdatePlan(Guid organizationId, AssignPlanRequest request)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        if (request.PlanId is Guid planId && await _planRepository.GetByIdAsync(planId) is null)
        {
            return BadRequest(new { message = "No plan with this id." });
        }

        await _tenantRepository.UpdatePlanAsync(organizationId, request.PlanId);
        return NoContent();
    }

    // Manual escape hatch for CRM's subscription-status cache (SuccessTtl 15 minutes,
    // PlatformSubscriptionStatusProvider) -- confirmed necessary by a real incident: setting
    // a subscription for a tenant here doesn't itself tell CRM anything, since only the
    // tenant-migration flow currently calls this invalidator. A PlatformAdmin who just
    // changed a subscription/plan and wants it visible in CRM immediately (rather than
    // waiting out the TTL) hits this instead of waiting or restarting CRM.
    [Authorize(Roles = "PlatformAdmin")]
    [HttpPost("{organizationId:guid}/crm-cache/invalidate")]
    public async Task<IActionResult> InvalidateCrmCache(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        var authorizationHeader = Request.Headers.Authorization.FirstOrDefault();
        await _crmCacheInvalidator.InvalidateAsync(organizationId, authorizationHeader, cancellationToken);
        return NoContent();
    }

    private static object BuildSubscriptionSummary(Core.Models.Subscription? record) => new
    {
        status = SubscriptionStatusCalculator.Compute(record?.EndDate, record?.GracePeriodDays ?? 0),
        startDate = record?.StartDate,
        endDate = record?.EndDate,
        gracePeriodDays = record?.GracePeriodDays,
        notes = record?.Notes,
    };

    private static object? BuildPlanSummary(Core.Models.Plan? plan) => plan is null
        ? null
        : new { id = plan.Id, name = plan.Name, maxUsers = plan.MaxUsers, maxRecords = plan.MaxRecords, priceMonthly = plan.PriceMonthly };
}
