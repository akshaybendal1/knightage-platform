namespace Knightage.Platform.Core.Models;

public record TenantProvisioningResult(Tenant Tenant, IReadOnlyList<TenantServiceDatabase> Databases);
