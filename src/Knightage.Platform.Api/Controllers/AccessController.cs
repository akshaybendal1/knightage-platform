using System.Security.Claims;
using Knightage.Platform.Core.Access;
using Knightage.Platform.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Knightage.Platform.Api.Controllers;

public record SaveUserAccessRequest(Dictionary<string, bool>? Overrides);

/// <summary>
/// Per-user access to the Knightage apps. The Owner and Admins of an organization manage it on the Users
/// screen (catalog, users, users/{id}); every app's server asks "me" for the signed-in user, forwarding
/// their token, and enforces the answer itself.
///
/// Only exceptions to the role defaults are stored. The users themselves live in knightage-identity (the
/// screen reads and changes them there directly); rows here are keyed by the caller's own organization,
/// so whatever id an admin sends can only ever affect a user of their own organization.
/// </summary>
[Authorize]
[ApiController]
[Route("api/access")]
public class AccessController : ControllerBase
{
    private readonly IUserAccessRepository _repository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;
    private readonly ICrmRoleAccessClient _crmRoleAccessClient;
    private readonly ILogger<AccessController> _logger;

    public AccessController(
        IUserAccessRepository repository,
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        ICrmRoleAccessClient crmRoleAccessClient,
        ILogger<AccessController> logger)
    {
        _repository = repository;
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _crmRoleAccessClient = crmRoleAccessClient;
        _logger = logger;
    }

    /// <summary>
    /// The signed-in user's apps and exceptions. Called by each app's server on behalf of the user (and by
    /// the service launcher). The Owner always gets every app and no exceptions, whatever is stored.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine()
    {
        if (!TryGetOrganizationId(out var organizationId) || !TryGetUserId(out var userId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var isOwner = role == AccessCatalog.Owner;
        var overrides = isOwner ? new Dictionary<string, bool>() : ToMap(await ReadForUserAsync(organizationId, userId));
        return Ok(new
        {
            role,
            isOwner,
            apps = AccessCatalog.Apps.ToDictionary(a => a.Service, a => isOwner || !overrides.TryGetValue(a.Service, out var allowed) || allowed),
            overrides = overrides.Where(o => o.Key.Contains(':')).ToDictionary(o => o.Key, o => o.Value),
        });
    }

    /// <summary>The apps and areas the Users screen shows, with each role's defaults for this organization.</summary>
    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        IReadOnlyList<Core.Models.TenantServiceDatabase> databases = tenant is null ? [] : await _databaseRepository.GetByTenantIdAsync(tenant.Id);
        bool Enabled(string service) => databases.Any(d =>
            string.Equals(d.ServiceName, service, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(d.Status, "Provisioned", StringComparison.OrdinalIgnoreCase));

        // CRM's role defaults are whatever its Access Control screen says for this organization.
        var crmRoles = Enabled("Crm") ? await _crmRoleAccessClient.GetRoleScreensAsync(Request.Headers.Authorization.ToString(), cancellationToken) : null;

        return Ok(new
        {
            roles = AccessCatalog.ConfigurableRoles,
            apps = AccessCatalog.Apps.Select(app => new
            {
                service = app.Service,
                displayName = app.DisplayName,
                enabled = Enabled(app.Service),
                roleDefaultsFrom = app.Service == "Crm" ? (crmRoles is null ? "built-in" : "crm-access-control") : "built-in",
                features = app.Features.Select(feature => new
                {
                    key = feature.Key,
                    label = feature.Label,
                    group = feature.Group,
                    actions = feature.Actions.Select(a => new { key = a.Key, label = a.Label }),
                    defaults = AccessCatalog.ConfigurableRoles.ToDictionary(
                        role => role,
                        role => app.Service == "Crm" && crmRoles is not null && crmRoles.TryGetValue(role, out var screens) && screens.TryGetValue(feature.Key, out var allowed)
                            ? allowed
                            : feature.DefaultFor(role)),
                }),
            }),
        });
    }

    /// <summary>Every user of the organization who has exceptions, with their app exceptions (for the list).</summary>
    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        if (!TryGetOrganizationId(out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        IReadOnlyList<UserAccessOverride> rows;
        try
        {
            rows = await _repository.GetForOrganizationAsync(organizationId);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(ex, "UserAccessOverrides is missing (sql/009 not applied?).");
            rows = [];
        }

        return Ok(new
        {
            items = rows.GroupBy(r => r.UserId).Select(g => new
            {
                userId = g.Key,
                apps = g.Where(r => !r.AccessKey.Contains(':')).ToDictionary(r => r.AccessKey, r => r.IsAllowed),
                areaExceptions = g.Count(r => r.AccessKey.Contains(':')),
                updatedAtUtc = g.Max(r => r.UpdatedAtUtc),
            }),
        });
    }

    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        if (!TryGetOrganizationId(out var organizationId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        var rows = await ReadForUserAsync(organizationId, userId);
        return Ok(new
        {
            userId,
            overrides = ToMap(rows),
            updatedAtUtc = rows.Count == 0 ? (DateTime?)null : rows.Max(r => r.UpdatedAtUtc),
            updatedByUserId = rows.OrderByDescending(r => r.UpdatedAtUtc).FirstOrDefault()?.UpdatedByUserId,
        });
    }

    /// <summary>
    /// Replaces a user's exceptions (an empty set = back to the role's defaults). POST, not PUT: the
    /// production host blocks PUT/DELETE. Nobody changes their own access -- an Admin could otherwise lock
    /// themselves out of this very screen's data in the apps -- and the Owner's would be ignored anyway.
    /// </summary>
    [Authorize(Roles = "Owner,Admin")]
    [HttpPost("users/{userId:guid}")]
    public async Task<IActionResult> SaveUser(Guid userId, SaveUserAccessRequest request)
    {
        if (!TryGetOrganizationId(out var organizationId) || !TryGetUserId(out var actingUserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No organization is associated with this account." });
        }

        if (userId == actingUserId)
        {
            return BadRequest(new { message = "You can't change your own access. Ask the owner or another admin." });
        }

        var overrides = request.Overrides ?? new Dictionary<string, bool>();
        if (overrides.Count > 500)
        {
            return BadRequest(new { message = "Too many entries." });
        }

        var unknown = overrides.Keys.Where(k => !AccessCatalog.IsValidKey(k)).ToList();
        if (unknown.Count > 0)
        {
            return BadRequest(new { message = $"Unknown access: {string.Join(", ", unknown.Take(5))}." });
        }

        try
        {
            await _repository.ReplaceForUserAsync(organizationId, userId, overrides, actingUserId);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogError(ex, "UserAccessOverrides is missing (sql/009 not applied?).");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "User access can't be saved yet: the Platform database hasn't been updated (sql/009)." });
        }

        return NoContent();
    }

    /// <summary>
    /// A user's stored rows, or none when the table doesn't exist yet (code deployed before sql/009): then
    /// everyone simply keeps their role's defaults instead of every app failing for every user.
    /// </summary>
    private async Task<IReadOnlyList<UserAccessOverride>> ReadForUserAsync(Guid organizationId, Guid userId)
    {
        try
        {
            return await _repository.GetForUserAsync(organizationId, userId);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(ex, "UserAccessOverrides is missing (sql/009 not applied?); using role defaults.");
            return [];
        }
    }

    private static Dictionary<string, bool> ToMap(IEnumerable<UserAccessOverride> rows) =>
        rows.Where(r => AccessCatalog.IsValidKey(r.AccessKey)).ToDictionary(r => r.AccessKey, r => r.IsAllowed);

    private bool TryGetOrganizationId(out Guid organizationId) =>
        Guid.TryParse(User.FindFirst("org_id")?.Value, out organizationId);

    // The JWT handler maps "sub" to NameIdentifier unless inbound claim mapping is off -- accept either.
    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out userId);
}
