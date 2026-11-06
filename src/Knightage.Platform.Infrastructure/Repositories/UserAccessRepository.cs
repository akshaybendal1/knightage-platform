using System.Data;
using Dapper;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Infrastructure.Data;

namespace Knightage.Platform.Infrastructure.Repositories;

public class UserAccessRepository : IUserAccessRepository
{
    private const string Columns = "UserId, AccessKey, IsAllowed, UpdatedAtUtc, UpdatedByUserId";

    private readonly DapperContext _context;

    public UserAccessRepository(DapperContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserAccessOverride>> GetForUserAsync(Guid organizationId, Guid userId)
    {
        const string sql = $"SELECT {Columns} FROM UserAccessOverrides WHERE OrganizationId = @OrganizationId AND UserId = @UserId";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<UserAccessOverride>(sql, new { OrganizationId = organizationId, UserId = userId })).ToList();
    }

    public async Task<IReadOnlyList<UserAccessOverride>> GetForOrganizationAsync(Guid organizationId)
    {
        const string sql = $"SELECT {Columns} FROM UserAccessOverrides WHERE OrganizationId = @OrganizationId";
        using var connection = _context.CreateConnection();
        return (await connection.QueryAsync<UserAccessOverride>(sql, new { OrganizationId = organizationId })).ToList();
    }

    public async Task ReplaceForUserAsync(Guid organizationId, Guid userId, IReadOnlyDictionary<string, bool> overrides, Guid? updatedByUserId)
    {
        using var connection = _context.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        await connection.ExecuteAsync(
            "DELETE FROM UserAccessOverrides WHERE OrganizationId = @OrganizationId AND UserId = @UserId",
            new { OrganizationId = organizationId, UserId = userId }, transaction);

        if (overrides.Count > 0)
        {
            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(
                @"INSERT INTO UserAccessOverrides (OrganizationId, UserId, AccessKey, IsAllowed, UpdatedAtUtc, UpdatedByUserId)
                  VALUES (@OrganizationId, @UserId, @AccessKey, @IsAllowed, @UpdatedAtUtc, @UpdatedByUserId)",
                overrides.Select(o => new
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    AccessKey = o.Key,
                    IsAllowed = o.Value,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = updatedByUserId,
                }),
                transaction);
        }

        transaction.Commit();
    }
}
