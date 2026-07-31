using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface IProvisioningService
{
    /// <summary>
    /// Ensures a tenant exists for the given organization and that every known business
    /// service has an isolated database provisioned for it. Idempotent -- safe to call again
    /// for the same organization (e.g. a retry after a partial failure); already-provisioned
    /// services are left untouched.
    /// </summary>
    Task<TenantProvisioningResult> ProvisionAsync(Guid organizationId, string organizationName, CancellationToken cancellationToken = default);
}
