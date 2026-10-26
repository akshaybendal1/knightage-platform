namespace Knightage.Platform.Core.TenantScaling;

/// <summary>
/// Tenant-scoped tables for the two services that stay common/shared for every tenant
/// (never migrated to a dedicated database) but whose per-org rows still need to come out
/// in a full export bundle -- see TenantMigrationController.ExportFullBundle. Split by
/// filter column, not just by service: unlike CRM's tables, these don't all key off
/// OrganizationId directly (confirmed against each table's actual schema -- see
/// TenantDataService for how the column is used).
/// </summary>
public static class OtherServiceTenantTables
{
    // Organizations is keyed by its own Id, which IS the organization id -- there's no
    // separate OrganizationId column on the row that describes the organization itself.
    public static readonly IReadOnlyList<string> IdentityFilteredById = new[] { "Organizations" };

    // Users has a normal OrganizationId FK column, same convention as every CRM table.
    public static readonly IReadOnlyList<string> IdentityFilteredByOrganizationId = new[] { "Users" };

    // Tenants.OrganizationId is the FK to Identity's Organizations.Id.
    public static readonly IReadOnlyList<string> PlatformFilteredByOrganizationId = new[] { "Tenants" };

    // Subscriptions and TenantServiceDatabases key off Platform's OWN Tenants.Id, not the
    // OrganizationId used everywhere else -- callers need that Id (via ITenantRepository),
    // not the organizationId route parameter, to filter these two correctly.
    // Plans is deliberately excluded -- it's a shared pricing catalog, not tenant data.
    public static readonly IReadOnlyList<string> PlatformFilteredByTenantId = new[] { "Subscriptions", "TenantServiceDatabases" };
}
