namespace Knightage.Platform.Core.Interfaces;

/// <summary>
/// Tells knightage-crm to drop its cached subscription/routing details for a tenant right
/// away, instead of waiting out its own cache TTL (15 minutes) -- called by
/// TenantMigrationController right when a CRM tenant migration starts and again when it
/// finishes, so the maintenance-window block and the repointed database actually take
/// effect on CRM's very next request for that tenant. Confirmed necessary by a real
/// incident: without this, a write made shortly after a successful migration (on a CRM
/// process whose cache was already warm for that tenant) silently landed in the shared
/// database that the migration's own purge step then deleted -- the write never reached the
/// dedicated database and was permanently lost.
/// </summary>
public interface ICrmCacheInvalidator
{
    /// <summary>
    /// Best-effort -- never throws. A failed call just means CRM keeps routing that tenant
    /// by its old, cached details until its own TTL naturally expires; it must never fail
    /// or block the migration itself, which already leaves the tenant fully consistent
    /// (correct database, correct bookkeeping) regardless of whether CRM's cache has caught
    /// up yet.
    /// </summary>
    Task InvalidateAsync(Guid organizationId, string? authorizationHeader, CancellationToken cancellationToken = default);
}
