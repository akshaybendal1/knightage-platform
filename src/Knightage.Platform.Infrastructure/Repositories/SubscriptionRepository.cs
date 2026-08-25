using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly DapperContext _context;

    public SubscriptionRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<Subscription?> GetByTenantIdAsync(Guid tenantId)
    {
        const string sql = @"SELECT Id, TenantId, StartDate, EndDate, GracePeriodDays, Notes, CreatedAtUtc, UpdatedAtUtc
                              FROM Subscriptions WHERE TenantId = @TenantId";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Subscription>(sql, new { TenantId = tenantId });
    }

    public async Task<Subscription> UpsertAsync(Subscription subscription)
    {
        using var connection = _context.CreateConnection();

        var existing = await connection.QuerySingleOrDefaultAsync<Subscription>(
            "SELECT Id, TenantId, StartDate, EndDate, GracePeriodDays, Notes, CreatedAtUtc, UpdatedAtUtc FROM Subscriptions WHERE TenantId = @TenantId",
            new { subscription.TenantId });

        if (existing is null)
        {
            subscription.Id = Guid.NewGuid();
            subscription.CreatedAtUtc = DateTime.UtcNow;
            subscription.UpdatedAtUtc = subscription.CreatedAtUtc;

            const string insertSql = @"INSERT INTO Subscriptions (Id, TenantId, StartDate, EndDate, GracePeriodDays, Notes, CreatedAtUtc, UpdatedAtUtc)
                                        VALUES (@Id, @TenantId, @StartDate, @EndDate, @GracePeriodDays, @Notes, @CreatedAtUtc, @UpdatedAtUtc)";
            await connection.ExecuteAsync(insertSql, subscription);
            return subscription;
        }

        subscription.Id = existing.Id;
        subscription.CreatedAtUtc = existing.CreatedAtUtc;
        subscription.UpdatedAtUtc = DateTime.UtcNow;

        const string updateSql = @"UPDATE Subscriptions
                                    SET StartDate = @StartDate, EndDate = @EndDate, GracePeriodDays = @GracePeriodDays,
                                        Notes = @Notes, UpdatedAtUtc = @UpdatedAtUtc
                                    WHERE Id = @Id";
        await connection.ExecuteAsync(updateSql, subscription);
        return subscription;
    }
}
