namespace Knightage.Platform.Core.Interfaces;

/// <summary>One stored exception to a user's role defaults: "Crm" (may open CRM) or "Crm:leads:add".</summary>
public record UserAccessOverride(Guid UserId, string AccessKey, bool IsAllowed, DateTime UpdatedAtUtc, Guid? UpdatedByUserId);

public interface IUserAccessRepository
{
    Task<IReadOnlyList<UserAccessOverride>> GetForUserAsync(Guid organizationId, Guid userId);

    Task<IReadOnlyList<UserAccessOverride>> GetForOrganizationAsync(Guid organizationId);

    /// <summary>Replaces the user's whole set atomically. An empty set puts them back on their role's defaults.</summary>
    Task ReplaceForUserAsync(Guid organizationId, Guid userId, IReadOnlyDictionary<string, bool> overrides, Guid? updatedByUserId);
}
