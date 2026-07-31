using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface ITenantRepository
{
    Task<IReadOnlyList<Tenant>> GetAllAsync();
    Task<Tenant?> GetByIdAsync(Guid id);
    Task<Tenant?> GetByOrganizationIdAsync(Guid organizationId);
    Task<Tenant> CreateAsync(Tenant tenant);
}
