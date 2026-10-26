using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface ITenantRepository
{
    Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, TenantListQuery query);
    Task<Tenant?> GetByIdAsync(Guid id);
    Task<Tenant?> GetByOrganizationIdAsync(Guid organizationId);
    Task<Tenant> CreateAsync(Tenant tenant);
    Task UpdateAccentColorAsync(Guid organizationId, string? accentColor);
    Task UpdateFieldForceTrackingEnabledAsync(Guid organizationId, bool enabled);
    Task UpdateAccountingMultiCompanyEnabledAsync(Guid organizationId, bool enabled);
    Task UpdatePlanAsync(Guid organizationId, Guid? planId);
    Task UpdateIsMigratingAsync(Guid organizationId, bool isMigrating);
}
