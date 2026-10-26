using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface IProvisioningService
{
    /// <summary>
    /// The fixed shared database name every tenant's Crm entry points at until it migrates
    /// to a dedicated database -- callers that need to tell "still shared" apart from
    /// "already dedicated" (e.g. refusing to re-migrate an already-migrated tenant) compare
    /// TenantServiceDatabases' current DatabaseName against this, rather than duplicating
    /// the literal.
    /// </summary>
    string CrmSharedDatabaseName { get; }

    /// <summary>
    /// Ensures a tenant exists for the given organization and that every known business
    /// service has an isolated database provisioned for it. Idempotent -- safe to call again
    /// for the same organization (e.g. a retry after a partial failure); already-provisioned
    /// services are left untouched.
    /// </summary>
    Task<TenantProvisioningResult> ProvisionAsync(Guid organizationId, string organizationName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one service's "Provisioned" row for a tenant that doesn't have it yet (older tenants
    /// predate Accounting/InventorySales being provisioned at sign-up). Returns false when the row
    /// already exists -- its status is then left alone for the caller to set.
    /// </summary>
    Task<bool> AddServiceAsync(Tenant tenant, string serviceKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a connection string for an arbitrary CRM-schema database -- same credentials
    /// as CrmShared always (confirmed: every server this app talks to uses the same login),
    /// but the server and database can both be overridden. A null/empty serverAddress means
    /// "the same server as CrmShared" -- the common case of just moving a tenant to another
    /// database on the server everyone's already on.
    /// </summary>
    string BuildCrmConnectionString(string? serverAddress, string databaseName);

    /// <summary>
    /// Whether a database with this name is currently reachable on the given server (or on
    /// CrmShared's own server, if serverAddress is null/empty) using this app's own
    /// credentials. This app never creates databases for CRM tenant moves -- confirmed
    /// against production that its login has no CREATE DATABASE rights, and the sharding
    /// model (many tenants can share one target database) means "create a fresh one" isn't
    /// even always the right action anyway. A PlatformAdmin must provision the target
    /// database (and grant this app's login access to it) before moving a tenant into it;
    /// this check exists so that's reported clearly upfront rather than failing confusingly
    /// partway through a copy.
    /// </summary>
    Task<bool> CrmDatabaseExistsAsync(string? serverAddress, string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies CRM's full current schema (via the bundled crm-schema/crm-full-schema.sql) to
    /// the database at this connection string, but ONLY if it doesn't already have it --
    /// checked by looking for the Leads table. A target that already has CRM's schema is
    /// either a shard already holding other tenants' data, or a database being re-targeted
    /// after a previous move -- either way, re-running CREATE TABLE against it would fail
    /// and risks nothing to skip.
    /// </summary>
    Task EnsureCrmSchemaAsync(string connectionString, CancellationToken cancellationToken = default);
}
