using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface ITenantMigrationRepository
{
    Task<TenantMigration> CreateAsync(TenantMigration migration);
    Task<TenantMigration?> GetLatestAsync(Guid tenantId, string serviceName);
    Task MarkPurgedAsync(Guid id);
}
