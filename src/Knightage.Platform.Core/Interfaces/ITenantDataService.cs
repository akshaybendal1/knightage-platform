namespace Knightage.Platform.Core.Interfaces;

/// <summary>
/// Exports/deletes one tenant's rows across a known, fixed list of tenant-scoped tables in
/// a given database -- the core primitive behind both the dedicated-database migration flow
/// and the full export bundle. Generic over the table list, connection, and filter column so
/// the same implementation serves CRM, Identity, and Platform's own database alike. Callers
/// pass tables in the order they want them iterated (import order for export, reverse-import
/// order for delete) -- this service doesn't know or care which app owns the tables.
///
/// filterColumn defaults to "OrganizationId" since that's true of most tenant-scoped tables
/// in this codebase, but it isn't universal: Identity's Organizations table is keyed by its
/// own Id (which IS the organization id, not a separate FK column), and Platform's
/// Subscriptions/TenantServiceDatabases are keyed by TenantId (Platform's own Tenants.Id,
/// a different value than the OrganizationId used everywhere else) -- callers for those
/// tables must pass the matching column name and filter value explicitly.
/// </summary>
public interface ITenantDataService
{
    /// <summary>
    /// Generates a SQL script of INSERT statements for every row in every listed table
    /// where filterColumn = filterValue, in the given table order. Read-only -- never
    /// touches the source database.
    /// </summary>
    Task<string> ExportAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a SQL script (as produced by ExportAsync, or any other batch of statements
    /// separated by standalone "GO" lines) against the target connection.
    /// </summary>
    Task ExecuteScriptAsync(string connectionString, string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every row where filterColumn = filterValue from every listed table, in the
    /// given order -- pass tables in reverse dependency order (children before parents)
    /// to avoid FK violations.
    /// </summary>
    Task DeleteAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-table row counts where filterColumn = filterValue -- used to verify a copy
    /// matches its source before anything depends on it.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountRowsAsync(string connectionString, IReadOnlyList<string> tables, Guid filterValue, string filterColumn = "OrganizationId", CancellationToken cancellationToken = default);
}
