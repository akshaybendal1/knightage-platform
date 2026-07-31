using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Service;

public class ProvisioningService : IProvisioningService
{
    private static readonly string[] KnownServices = ["Accounting", "Crm", "InventorySales"];

    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;
    private readonly IServiceDatabaseProvisioner _databaseProvisioner;

    public ProvisioningService(
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        IServiceDatabaseProvisioner databaseProvisioner)
    {
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _databaseProvisioner = databaseProvisioner;
    }

    public async Task<TenantProvisioningResult> ProvisionAsync(Guid organizationId, string organizationName, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        tenant ??= await _tenantRepository.CreateAsync(new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = organizationName,
            Slug = Slugify(organizationName),
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
        });

        var databases = (await _databaseRepository.GetByTenantIdAsync(tenant.Id)).ToList();

        foreach (var serviceName in KnownServices)
        {
            if (databases.Any(d => string.Equals(d.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var databaseName = $"Knightage_{serviceName}_{tenant.Slug}";
            await _databaseProvisioner.ProvisionAsync(databaseName, serviceName, cancellationToken);

            var record = await _databaseRepository.CreateAsync(new TenantServiceDatabase
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceName = serviceName,
                DatabaseName = databaseName,
                Status = "Provisioned",
                CreatedAtUtc = DateTime.UtcNow,
            });
            databases.Add(record);
        }

        return new TenantProvisioningResult(tenant, databases);
    }

    private static string Slugify(string name)
    {
        var chars = name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var slug = new string(chars);
        while (slug.Contains("__"))
        {
            slug = slug.Replace("__", "_");
        }
        return slug.Trim('_');
    }
}
