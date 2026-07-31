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

    public async Task<IReadOnlyList<Tenant>> GetAllAsync()
    {
        const string sql = @"SELECT Id, OrganizationId, Name, Slug, Status, CreatedAtUtc
                              FROM Tenants ORDER BY CreatedAtUtc DESC";
        using var connection = _context.CreateConnection();
        var tenants = await connection.QueryAsync<Tenant>(sql);
        return tenants.ToList();
    }

    public async Task<Tenant?> GetByIdAsync(Guid id)
    {
        const string sql = @"SELECT Id, OrganizationId, Name, Slug, Status, CreatedAtUtc
                              FROM Tenants WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Tenant>(sql, new { Id = id });
    }

    public async Task<Tenant?> GetByOrganizationIdAsync(Guid organizationId)
    {
        const string sql = @"SELECT Id, OrganizationId, Name, Slug, Status, CreatedAtUtc
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
}
