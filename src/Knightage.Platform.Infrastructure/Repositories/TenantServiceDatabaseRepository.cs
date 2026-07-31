using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class TenantServiceDatabaseRepository : ITenantServiceDatabaseRepository
{
    private readonly DapperContext _context;

    public TenantServiceDatabaseRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TenantServiceDatabase>> GetByTenantIdAsync(Guid tenantId)
    {
        const string sql = @"SELECT Id, TenantId, ServiceName, DatabaseName, Status, CreatedAtUtc
                              FROM TenantServiceDatabases WHERE TenantId = @TenantId ORDER BY ServiceName";
        using var connection = _context.CreateConnection();
        var databases = await connection.QueryAsync<TenantServiceDatabase>(sql, new { TenantId = tenantId });
        return databases.ToList();
    }

    public async Task<TenantServiceDatabase> CreateAsync(TenantServiceDatabase database)
    {
        const string sql = @"INSERT INTO TenantServiceDatabases (Id, TenantId, ServiceName, DatabaseName, Status, CreatedAtUtc)
                              VALUES (@Id, @TenantId, @ServiceName, @DatabaseName, @Status, @CreatedAtUtc)";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, database);
        return database;
    }
}
