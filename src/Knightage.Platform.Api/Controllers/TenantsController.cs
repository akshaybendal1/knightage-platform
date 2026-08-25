using Knightage.Platform.Api.Contracts;
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

    public TenantsController(
        IProvisioningService provisioningService,
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        ISubscriptionRepository subscriptionRepository)
    {
        _provisioningService = provisioningService;
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _subscriptionRepository = subscriptionRepository;
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
    public async Task<IActionResult> GetAll()
    {
        var tenants = await _tenantRepository.GetAllAsync();
        var rows = new List<object>(tenants.Count);
        foreach (var tenant in tenants)
        {
            var subscriptionRecord = await _subscriptionRepository.GetByTenantIdAsync(tenant.Id);
            rows.Add(new { tenant, subscription = BuildSubscriptionSummary(subscriptionRecord) });
        }

        return Ok(rows);
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

        return Ok(new { tenant, databases, subscription = BuildSubscriptionSummary(subscriptionRecord) });
    }

    private static object BuildSubscriptionSummary(Core.Models.Subscription? record) => new
    {
        status = SubscriptionStatusCalculator.Compute(record?.EndDate, record?.GracePeriodDays ?? 0),
        startDate = record?.StartDate,
        endDate = record?.EndDate,
        gracePeriodDays = record?.GracePeriodDays,
        notes = record?.Notes,
    };
}
