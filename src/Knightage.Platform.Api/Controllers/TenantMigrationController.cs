using Knightage.Platform.Api.Contracts;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Core.TenantScaling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

// Moving a CRM tenant off the shared database onto one of its own -- see the plan this
// was built from for the full design. Everything here is synchronous (a single blocking
// request per action); fine for realistic tenant data sizes, a background job is a future
// refinement if a tenant ever gets big enough to need one. PlatformAdmin only throughout --
// this operates on an explicit target organizationId, not the caller's own org.
[Authorize(Roles = "PlatformAdmin")]
[ApiController]
[Route("api/tenants/{organizationId:guid}/migration")]
public class TenantMigrationController : ControllerBase
{
    private const string CrmServiceName = "Crm";

    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;
    private readonly ITenantMigrationRepository _migrationRepository;
    private readonly IProvisioningService _provisioningService;
    private readonly ITenantDataService _tenantDataService;
    private readonly ICrmCacheInvalidator _crmCacheInvalidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantMigrationController> _logger;

    public TenantMigrationController(
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        ITenantMigrationRepository migrationRepository,
        IProvisioningService provisioningService,
        ITenantDataService tenantDataService,
        ICrmCacheInvalidator crmCacheInvalidator,
        IConfiguration configuration,
        ILogger<TenantMigrationController> logger)
    {
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _migrationRepository = migrationRepository;
        _provisioningService = provisioningService;
        _tenantDataService = tenantDataService;
        _crmCacheInvalidator = crmCacheInvalidator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("crm/export")]
    public async Task<IActionResult> ExportCrmData(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        var connectionString = await ResolveCrmConnectionStringAsync(tenant.Id);
        var script = await _tenantDataService.ExportAsync(connectionString, CrmTenantTables.InOrder, organizationId, cancellationToken: cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(script), "application/sql", $"crm-export-{organizationId}.sql");
    }

    [HttpGet("export-bundle")]
    public async Task<IActionResult> ExportFullBundle(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        var identityConnectionString = _configuration.GetConnectionString("IdentityShared")
            ?? throw new InvalidOperationException("ConnectionStrings:IdentityShared is not configured.");
        var platformConnectionString = _configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        var crmConnectionString = await ResolveCrmConnectionStringAsync(tenant.Id);

        var header = $"""
            -- Full tenant export bundle for organization {organizationId}, generated {DateTime.UtcNow:u}.
            -- Prerequisite: a fresh Identity + Platform + CRM deployment must already have its own
            -- sql/ migrations applied (this gives them their schema) before running this script --
            -- this is data only, in dependency order: Identity, then Platform, then CRM.

            """;

        // Organizations is keyed by its own Id (which IS the organization id) -- Users has
        // a normal OrganizationId FK column. See OtherServiceTenantTables for why these two
        // can't share one ExportAsync call the way CRM's tables can.
        var organizationsScript = await _tenantDataService.ExportAsync(identityConnectionString, OtherServiceTenantTables.IdentityFilteredById, organizationId, "Id", cancellationToken);
        var usersScript = await _tenantDataService.ExportAsync(identityConnectionString, OtherServiceTenantTables.IdentityFilteredByOrganizationId, organizationId, cancellationToken: cancellationToken);
        var identityScript = organizationsScript + usersScript;

        // Subscriptions/TenantServiceDatabases key off Platform's own Tenants.Id, not the
        // OrganizationId route parameter -- that's tenant.Id here, a different value.
        var tenantsScript = await _tenantDataService.ExportAsync(platformConnectionString, OtherServiceTenantTables.PlatformFilteredByOrganizationId, organizationId, cancellationToken: cancellationToken);
        var tenantScopedScript = await _tenantDataService.ExportAsync(platformConnectionString, OtherServiceTenantTables.PlatformFilteredByTenantId, tenant.Id, "TenantId", cancellationToken);
        var platformScript = tenantsScript + tenantScopedScript;

        // Uses whichever database Crm currently points at for this tenant -- the shared
        // Knightage_Crm for a tenant that hasn't migrated, or its dedicated database if it
        // has. Exporting always from the shared database would silently produce an empty
        // CRM section for an already-migrated tenant, exactly the tenant this bundle is
        // most likely to be requested for (moving them to their own standalone deployment).
        var crmScript = await _tenantDataService.ExportAsync(crmConnectionString, CrmTenantTables.InOrder, organizationId, cancellationToken: cancellationToken);

        var bundle = header + "\n-- ===== Identity =====\n" + identityScript + "\n-- ===== Platform =====\n" + platformScript + "\n-- ===== CRM =====\n" + crmScript;
        return File(System.Text.Encoding.UTF8.GetBytes(bundle), "application/sql", $"tenant-export-bundle-{organizationId}.sql");
    }

    [HttpPost("crm")]
    public async Task<IActionResult> MoveCrmData(Guid organizationId, MoveCrmDataRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        if (tenant.IsMigrating)
        {
            return Conflict(new { message = "A migration is already in progress for this organization." });
        }

        var databases = await _databaseRepository.GetByTenantIdAsync(tenant.Id);
        var crmDatabase = databases.FirstOrDefault(d => string.Equals(d.ServiceName, CrmServiceName, StringComparison.OrdinalIgnoreCase));
        if (crmDatabase is null)
        {
            return BadRequest(new { message = "This tenant has no CRM database provisioned yet." });
        }

        var targetServer = string.IsNullOrWhiteSpace(request.TargetServer) ? null : request.TargetServer.Trim();
        var targetDatabaseName = request.TargetDatabaseName.Trim();

        // Refuse a no-op move to the exact location this tenant is already at -- re-running
        // this would try to INSERT rows that already exist there and fail on the primary key.
        if (string.Equals(crmDatabase.ServerAddress ?? string.Empty, targetServer ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && string.Equals(crmDatabase.DatabaseName, targetDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "This tenant's CRM data is already on that database." });
        }

        // This app never creates databases (no CREATE DATABASE rights, and with sharding a
        // target can legitimately already exist with other tenants on it) -- the target must
        // already be reachable before anything else here starts, so a typo'd server/database
        // name or a missing grant is reported clearly upfront rather than after the
        // maintenance window has already begun.
        if (!await _provisioningService.CrmDatabaseExistsAsync(targetServer, targetDatabaseName, cancellationToken))
        {
            return BadRequest(new { message = $"Database '{targetDatabaseName}' on {(targetServer is null ? "the default server" : $"'{targetServer}'")} isn't reachable. Create it first (and make sure this app's login has access) before moving a tenant into it." });
        }

        var targetConnectionString = _provisioningService.BuildCrmConnectionString(targetServer, targetDatabaseName);
        var authorizationHeader = Request.Headers.Authorization.FirstOrDefault();

        await _tenantRepository.UpdateIsMigratingAsync(organizationId, true);
        // Tell CRM right away, not just after this finishes -- otherwise a CRM process whose
        // cache was already warm for this tenant (SuccessTtl 15 minutes) wouldn't see
        // IsMigrating until its own cache happened to expire, and could keep serving
        // requests against the old database throughout a window meant to be blocked.
        await _crmCacheInvalidator.InvalidateAsync(organizationId, authorizationHeader, cancellationToken);
        var dataCopiedThisAttempt = false;
        try
        {
            // No-ops if the target already has CRM's schema (an existing shard with other
            // tenants' data, or this tenant being re-targeted) -- see its own comment.
            await _provisioningService.EnsureCrmSchemaAsync(targetConnectionString, cancellationToken);

            var exportScript = await _tenantDataService.ExportAsync(CrmSharedConnectionString(), CrmTenantTables.InOrder, organizationId, cancellationToken: cancellationToken);
            await _tenantDataService.ExecuteScriptAsync(targetConnectionString, exportScript, cancellationToken);
            // Only reachable once the copy has actually landed -- from here on, a failure
            // means THIS tenant's rows are sitting at the target and need cleaning up, not
            // that the whole database (which we don't own and may hold other tenants) does.
            dataCopiedThisAttempt = true;

            var sourceCounts = await _tenantDataService.CountRowsAsync(CrmSharedConnectionString(), CrmTenantTables.InOrder, organizationId, cancellationToken: cancellationToken);
            // Filtered by OrganizationId same as the source, so this correctly ignores any
            // other tenants' rows already sitting in a shared target database.
            var destCounts = await _tenantDataService.CountRowsAsync(targetConnectionString, CrmTenantTables.InOrder, organizationId, cancellationToken: cancellationToken);
            var mismatches = sourceCounts.Where(kv => destCounts.GetValueOrDefault(kv.Key) != kv.Value).ToList();
            if (mismatches.Count > 0)
            {
                _logger.LogError("Tenant migration row-count mismatch for {OrganizationId}: {Mismatches}", organizationId,
                    string.Join(", ", mismatches.Select(m => $"{m.Key} source={m.Value} dest={destCounts.GetValueOrDefault(m.Key)}")));
                throw new InvalidOperationException("Row counts didn't match after copying.");
            }

            // Backup script of the shared-DB copy, taken now while it's still the same data
            // that was just copied -- the safety net before the purge step deletes it.
            var backupDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "tenant-backups", tenant.Id.ToString());
            Directory.CreateDirectory(backupDir);
            var backupFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}.sql";
            var backupFilePath = Path.Combine(backupDir, backupFileName);
            await System.IO.File.WriteAllTextAsync(backupFilePath, exportScript, cancellationToken);

            await _databaseRepository.UpdateDatabaseLocationAsync(tenant.Id, CrmServiceName, targetServer, targetDatabaseName);
            await _migrationRepository.CreateAsync(new TenantMigration
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceName = CrmServiceName,
                OldServerAddress = crmDatabase.ServerAddress,
                OldDatabaseName = crmDatabase.DatabaseName,
                NewServerAddress = targetServer,
                NewDatabaseName = targetDatabaseName,
                Status = TenantMigrationStatus.AwaitingPurge,
                BackupFilePath = backupFilePath,
                MigratedAtUtc = DateTime.UtcNow,
            });

            return Ok(new { targetServer, targetDatabaseName, rowCounts = destCounts });
        }
        catch (Exception ex)
        {
            // A failed move must never leave this tenant's rows half-copied into the target --
            // but the target database itself is never touched (dropped/recreated) here, since
            // this app didn't create it and it may already hold other tenants' data (sharding).
            // Only delete what THIS attempt actually inserted, and only if it got that far.
            _logger.LogError(ex, "CRM data move failed for organization {OrganizationId}.", organizationId);
            if (dataCopiedThisAttempt)
            {
                try
                {
                    await _tenantDataService.DeleteAsync(targetConnectionString, CrmTenantTables.InOrder.Reverse().ToList(), organizationId, cancellationToken: cancellationToken);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Failed to roll back partially-copied rows for organization {OrganizationId} at the target database after a failed move.", organizationId);
                }
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The move failed and was rolled back -- no data was deleted from or left orphaned at the source. Check the server log for details." });
        }
        finally
        {
            await _tenantRepository.UpdateIsMigratingAsync(organizationId, false);
            // Invalidate again regardless of outcome: on success CRM needs to see the new
            // database location immediately (the exact gap that caused a real data-loss
            // incident -- see ICrmCacheInvalidator); on failure CRM needs to see
            // IsMigrating flip back to false immediately rather than staying blocked until
            // its own cache expires, since the earlier invalidation call above means it may
            // now be caching IsMigrating=true.
            await _crmCacheInvalidator.InvalidateAsync(organizationId, authorizationHeader, cancellationToken);
        }
    }

    [HttpGet("crm/backup")]
    public async Task<IActionResult> DownloadBackup(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound();
        }

        var migration = await _migrationRepository.GetLatestAsync(tenant.Id, CrmServiceName);
        if (migration?.BackupFilePath is null || !System.IO.File.Exists(migration.BackupFilePath))
        {
            return NotFound(new { message = "No backup file is available for this tenant." });
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(migration.BackupFilePath, cancellationToken);
        return File(bytes, "application/sql", Path.GetFileName(migration.BackupFilePath));
    }

    [HttpPost("crm/purge")]
    public async Task<IActionResult> PurgeSharedData(Guid organizationId, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound();
        }

        var migration = await _migrationRepository.GetLatestAsync(tenant.Id, CrmServiceName);
        if (migration is null || migration.Status != TenantMigrationStatus.AwaitingPurge)
        {
            return BadRequest(new { message = "There's no completed migration awaiting purge for this tenant." });
        }

        // Purge from wherever this tenant's data actually moved FROM, not always the default
        // shared database -- under sharding, a tenant could have moved from one non-default
        // shard to another, in which case the old location isn't Knightage_Crm at all.
        var oldConnectionString = _provisioningService.BuildCrmConnectionString(migration.OldServerAddress, migration.OldDatabaseName);
        await _tenantDataService.DeleteAsync(oldConnectionString, CrmTenantTables.InOrder.Reverse().ToList(), organizationId, cancellationToken: cancellationToken);
        await _migrationRepository.MarkPurgedAsync(migration.Id);

        return NoContent();
    }

    // Same credentials as CrmShared, but pointed at whichever server+database
    // TenantServiceDatabases currently says Crm lives in for this tenant -- the default
    // shared database for a tenant that hasn't moved, or wherever it's been moved to
    // (possibly a different server entirely) if it has. Falls back to CrmShared unchanged if
    // the tenant has no Crm database recorded at all (shouldn't happen for a tenant with any
    // Crm data, but mirrors the null-check every other database lookup here already does
    // rather than assuming).
    private async Task<string> ResolveCrmConnectionStringAsync(Guid tenantId)
    {
        var databases = await _databaseRepository.GetByTenantIdAsync(tenantId);
        var crmDatabase = databases.FirstOrDefault(d => string.Equals(d.ServiceName, CrmServiceName, StringComparison.OrdinalIgnoreCase));
        return crmDatabase is null
            ? CrmSharedConnectionString()
            : _provisioningService.BuildCrmConnectionString(crmDatabase.ServerAddress, crmDatabase.DatabaseName);
    }

    private string CrmSharedConnectionString() =>
        _configuration.GetConnectionString("CrmShared") ?? throw new InvalidOperationException("ConnectionStrings:CrmShared is not configured.");
}
