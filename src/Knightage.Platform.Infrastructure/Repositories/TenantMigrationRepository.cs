using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class TenantMigrationRepository : ITenantMigrationRepository
{
    private readonly DapperContext _context;

    public TenantMigrationRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<TenantMigration> CreateAsync(TenantMigration migration)
    {
        const string sql = @"INSERT INTO TenantMigrations
                                  (Id, TenantId, ServiceName, OldServerAddress, OldDatabaseName, NewServerAddress, NewDatabaseName, Status, BackupFilePath, MigratedAtUtc, PurgedAtUtc)
                              VALUES (@Id, @TenantId, @ServiceName, @OldServerAddress, @OldDatabaseName, @NewServerAddress, @NewDatabaseName, @Status, @BackupFilePath, @MigratedAtUtc, @PurgedAtUtc)";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, migration);
        return migration;
    }

    public async Task<TenantMigration?> GetLatestAsync(Guid tenantId, string serviceName)
    {
        const string sql = @"SELECT TOP 1 Id, TenantId, ServiceName, OldServerAddress, OldDatabaseName, NewServerAddress, NewDatabaseName, Status, BackupFilePath, MigratedAtUtc, PurgedAtUtc
                              FROM TenantMigrations
                              WHERE TenantId = @TenantId AND ServiceName = @ServiceName
                              ORDER BY MigratedAtUtc DESC";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantMigration>(sql, new { TenantId = tenantId, ServiceName = serviceName });
    }

    public async Task MarkPurgedAsync(Guid id)
    {
        const string sql = @"UPDATE TenantMigrations SET Status = @Status, PurgedAtUtc = @PurgedAtUtc WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id, Status = Core.Models.TenantMigrationStatus.Purged, PurgedAtUtc = DateTime.UtcNow });
    }
}
