using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly DapperContext _context;

    public PlanRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Plan>> GetAllAsync(bool includeInactive = false)
    {
        var sql = @"SELECT Id, Name, MaxUsers, MaxRecords, PriceMonthly, IsActive, SortOrder, CreatedAtUtc
                     FROM Plans"
                  + (includeInactive ? "" : " WHERE IsActive = 1")
                  + " ORDER BY SortOrder, CreatedAtUtc";
        using var connection = _context.CreateConnection();
        var plans = await connection.QueryAsync<Plan>(sql);
        return plans.ToList();
    }

    public async Task<Plan?> GetByIdAsync(Guid id)
    {
        const string sql = @"SELECT Id, Name, MaxUsers, MaxRecords, PriceMonthly, IsActive, SortOrder, CreatedAtUtc
                              FROM Plans WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Plan>(sql, new { Id = id });
    }

    public async Task<Plan> CreateAsync(Plan plan)
    {
        const string sql = @"INSERT INTO Plans (Id, Name, MaxUsers, MaxRecords, PriceMonthly, IsActive, SortOrder, CreatedAtUtc)
                              VALUES (@Id, @Name, @MaxUsers, @MaxRecords, @PriceMonthly, @IsActive, @SortOrder, @CreatedAtUtc)";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, plan);
        return plan;
    }

    public async Task UpdateAsync(Plan plan)
    {
        const string sql = @"UPDATE Plans SET Name = @Name, MaxUsers = @MaxUsers, MaxRecords = @MaxRecords,
                              PriceMonthly = @PriceMonthly, SortOrder = @SortOrder WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, plan);
    }

    public async Task SetActiveAsync(Guid id, bool isActive)
    {
        const string sql = @"UPDATE Plans SET IsActive = @IsActive WHERE Id = @Id";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
    }
}
