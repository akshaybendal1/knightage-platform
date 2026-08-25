using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByTenantIdAsync(Guid tenantId);
    Task<Subscription> UpsertAsync(Subscription subscription);
}
