using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly DapperContext _context;

    public TenantRepository(DapperContext context)
    {
        _context = context;
    }

    // Subscription status is never stored -- SubscriptionStatusCalculator derives it from the
    // dates -- so to filter and sort on it in SQL the same rule is repeated here. Keep the two
    // in step: Active through EndDate, ReadOnly for GracePeriodDays after it, then Disabled;
    // no subscription row at all is NotSubscribed. Computed once per row in the APPLY below.
    private const string TenantListFrom = @"FROM Tenants t
                              LEFT JOIN Subscriptions s ON s.TenantId = t.Id
                              LEFT JOIN Plans p ON p.Id = t.PlanId
                              CROSS APPLY (SELECT CASE
                                  WHEN s.EndDate IS NULL THEN 'NotSubscribed'
                                  WHEN CAST(SYSUTCDATETIME() AS date) <= s.EndDate THEN 'Active'
                                  WHEN CAST(SYSUTCDATETIME() AS date) <= DATEADD(day, s.GracePeriodDays, s.EndDate) THEN 'ReadOnly'
                                  ELSE 'Disabled' END AS Status) sub";

    public async Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, TenantListQuery query)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            conditions.Add(@"(t.Name LIKE @Search ESCAPE '\' OR t.Slug LIKE @Search ESCAPE '\'
                              OR CONVERT(varchar(36), t.OrganizationId) LIKE @Search ESCAPE '\')");
            parameters.Add("Search", "%" + EscapeLike(search) + "%");
        }

        if (query.Plan == TenantListQuery.PlanNone)
        {
            conditions.Add("t.PlanId IS NULL");
        }
        else if (Guid.TryParse(query.Plan, out var planId))
        {
            conditions.Add("t.PlanId = @PlanId");
            parameters.Add("PlanId", planId);
        }

        if (!string.IsNullOrEmpty(query.SubscriptionStatus))
        {
            conditions.Add("sub.Status = @SubscriptionStatus");
            parameters.Add("SubscriptionStatus", query.SubscriptionStatus);
        }

        var where = conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions);

        // ORDER BY text comes only from this fixed map, never from the request, so the sort
        // key and direction can't inject SQL. Rows with no plan / no end date always sort last
        // instead of SQL Server's NULLs-first-on-ASC default. The trailing keys keep paging
        // stable when the chosen column has ties.
        var direction = query.Descending ? "DESC" : "ASC";
        var orderBy = query.SortBy switch
        {
            TenantListQuery.SortName => $"t.Name {direction}",
            TenantListQuery.SortSlug => $"t.Slug {direction}",
            TenantListQuery.SortPlan => $"CASE WHEN p.Name IS NULL THEN 1 ELSE 0 END, p.Name {direction}",
            TenantListQuery.SortSubscription =>
                $"CASE sub.Status WHEN 'Active' THEN 0 WHEN 'ReadOnly' THEN 1 WHEN 'Disabled' THEN 2 ELSE 3 END {direction}",
            TenantListQuery.SortEnds => $"CASE WHEN s.EndDate IS NULL THEN 1 ELSE 0 END, s.EndDate {direction}",
            _ => $"t.CreatedAtUtc {direction}",
        };

        // SQL Server rejects a column that appears twice in one ORDER BY, so when the chosen
        // sort already is creation time, only the Id tie-break is added.
        var tieBreak = query.SortBy is TenantListQuery.SortName or TenantListQuery.SortSlug or TenantListQuery.SortPlan
            or TenantListQuery.SortSubscription or TenantListQuery.SortEnds
            ? "t.CreatedAtUtc DESC, t.Id"
            : "t.Id";

        var countSql = $"SELECT COUNT(*) {TenantListFrom} {where}";
        var pagedSql = $@"SELECT t.Id, t.OrganizationId, t.Name, t.Slug, t.Status, t.CreatedAtUtc, t.AccentColor, t.FieldForceTrackingEnabled, t.AccountingMultiCompanyEnabled, t.PlanId, t.IsMigrating
                              {TenantListFrom} {where}
                              ORDER BY {orderBy}, {tieBreak}
                              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        using var connection = _context.CreateConnection();
        var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);
        var tenants = await connection.QueryAsync<Tenant>(pagedSql, parameters);
        return (tenants.ToList(), totalCount);
    }

    // The search text goes into a LIKE pattern, so the characters LIKE treats specially must be
    // escaped or a search for "100%" or "[a]" would match the wrong rows.
    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_").Replace("[", @"\[");

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        const string sql = @"SELECT Id, OrganizationId, Name, Slug, Status, CreatedAtUtc, AccentColor, FieldForceTrackingEnabled, AccountingMultiCompanyEnabled, PlanId, IsMigrating
                              FROM Tenants WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new { Id = id });
    }

    public async Task<Tenant?> GetByOrganizationIdAsync(Guid organizationId)
    {
        const string sql = @"SELECT Id, OrganizationId, Name, Slug, Status, CreatedAtUtc, AccentColor, FieldForceTrackingEnabled, AccountingMultiCompanyEnabled, PlanId, IsMigrating
                              FROM Tenants WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new { OrganizationId = organizationId });
    }

    public async Task<Tenant> CreateAsync(Tenant tenant)
    {
        const string sql = @"INSERT INTO Tenants (Id, OrganizationId, Name, Slug, Status, CreatedAtUtc)
                              VALUES (@Id, @OrganizationId, @Name, @Slug, @Status, @CreatedAtUtc)";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, tenant);
        return tenant;
    }

    public async Task UpdateAccentColorAsync(Guid organizationId, string? accentColor)
    {
        const string sql = @"UPDATE Tenants SET AccentColor = @AccentColor WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, AccentColor = accentColor });
    }

    public async Task UpdateFieldForceTrackingEnabledAsync(Guid organizationId, bool enabled)
    {
        const string sql = @"UPDATE Tenants SET FieldForceTrackingEnabled = @Enabled WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, Enabled = enabled });
    }

    public async Task UpdateAccountingMultiCompanyEnabledAsync(Guid organizationId, bool enabled)
    {
        const string sql = @"UPDATE Tenants SET AccountingMultiCompanyEnabled = @Enabled WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, Enabled = enabled });
    }

    public async Task UpdatePlanAsync(Guid organizationId, Guid? planId)
    {
        const string sql = @"UPDATE Tenants SET PlanId = @PlanId WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, PlanId = planId });
    }

    public async Task UpdateIsMigratingAsync(Guid organizationId, bool isMigrating)
    {
        const string sql = @"UPDATE Tenants SET IsMigrating = @IsMigrating WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { OrganizationId = organizationId, IsMigrating = isMigrating });
    }
}
