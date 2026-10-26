using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface ITenantServiceDatabaseRepository
{
    Task<IReadOnlyList<TenantServiceDatabase>> GetByTenantIdAsync(Guid tenantId);
    Task<TenantServiceDatabase> CreateAsync(TenantServiceDatabase database);
    Task UpdateDatabaseLocationAsync(Guid tenantId, string serviceName, string? serverAddress, string databaseName);
    Task<bool> UpdateStatusAsync(Guid tenantId, string serviceName, string status);
}
