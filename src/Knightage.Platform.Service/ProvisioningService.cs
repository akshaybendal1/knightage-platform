using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Knightage.Platform.Service;

public class ProvisioningService : IProvisioningService
{
    // Crm, Accounting, and InventorySales' databases are each a single fixed shared database
    // (Knightage_Crm / Knightage_Accounting / Knightage_InventorySales), created once
    // out-of-band -- see knightage-crm/sql/010_organization_id.sql,
    // knightage-accounting/sql/002_organization_id.sql, and
    // knightage-inventory-sales/sql/002_organization_id.sql. No CREATE DATABASE happens here;
    // this only records the bookkeeping row so this app's /tenants ops UI keeps showing
    // "<Service>: Provisioned" per tenant, and so each service's own tenant-resolution
    // middleware sees a "Provisioned" row for its ServiceName.
    private const string SharedCrmDatabaseName = "Knightage_Crm";
    private const string AccountingSharedDatabaseName = "Knightage_Accounting";
    private const string InventorySalesSharedDatabaseName = "Knightage_InventorySales";

    // Seeded once, only the first time a tenant's Crm database row is created (never
    // re-applied to an org that's already provisioned) -- previously a brand-new org
    // started with zero stages and had to use the "+ Add new stage..." quick-add
    // everywhere before Leads/Products were usable at all. Names/won-lost flags are a
    // reasonable generic starting point; PlatformAdmin or the org itself can rename,
    // reorder, or add more via the existing Pipeline Stages / Product Stages pages.
    private static readonly (string Name, bool IsWon, bool IsLost, bool IsDefault)[] DefaultPipelineStages =
    {
        ("New", false, false, true),
        ("Contacted", false, false, false),
        ("Qualified", false, false, false),
        ("Proposal Sent", false, false, false),
        ("Won", true, false, false),
        ("Lost", false, true, false),
    };

    private static readonly (string Name, bool IsWon, bool IsLost, bool IsDefault)[] DefaultProductStages =
    {
        ("Proposed", false, false, true),
        ("Approved", false, false, false),
        ("Delivered", true, false, false),
        ("Cancelled", false, true, false),
    };

    public string CrmSharedDatabaseName => SharedCrmDatabaseName;

    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;
    private readonly ITenantDataService _tenantDataService;
    private readonly string _crmSharedConnectionString;

    public ProvisioningService(
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository,
        ITenantDataService tenantDataService,
        IConfiguration configuration)
    {
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
        _tenantDataService = tenantDataService;
        _crmSharedConnectionString = configuration.GetConnectionString("CrmShared")
            ?? throw new InvalidOperationException("ConnectionStrings:CrmShared is not configured.");
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

        if (!databases.Any(d => string.Equals(d.ServiceName, "Crm", StringComparison.OrdinalIgnoreCase)))
        {
            var record = await _databaseRepository.CreateAsync(new TenantServiceDatabase
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceName = "Crm",
                DatabaseName = SharedCrmDatabaseName,
                Status = "Provisioned",
                CreatedAtUtc = DateTime.UtcNow,
            });
            databases.Add(record);
            await SeedDefaultCrmStagesAsync(organizationId, cancellationToken);
        }

        if (!databases.Any(d => string.Equals(d.ServiceName, "Accounting", StringComparison.OrdinalIgnoreCase)))
        {
            var record = await _databaseRepository.CreateAsync(new TenantServiceDatabase
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceName = "Accounting",
                DatabaseName = AccountingSharedDatabaseName,
                Status = "Provisioned",
                CreatedAtUtc = DateTime.UtcNow,
            });
            databases.Add(record);
        }

        if (!databases.Any(d => string.Equals(d.ServiceName, "InventorySales", StringComparison.OrdinalIgnoreCase)))
        {
            var record = await _databaseRepository.CreateAsync(new TenantServiceDatabase
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                ServiceName = "InventorySales",
                DatabaseName = InventorySalesSharedDatabaseName,
                Status = "Provisioned",
                CreatedAtUtc = DateTime.UtcNow,
            });
            databases.Add(record);
        }

        return new TenantProvisioningResult(tenant, databases);
    }

    public async Task<bool> AddServiceAsync(Tenant tenant, string serviceKey, CancellationToken cancellationToken = default)
    {
        var databaseName = serviceKey switch
        {
            "Crm" => SharedCrmDatabaseName,
            "Accounting" => AccountingSharedDatabaseName,
            "InventorySales" => InventorySalesSharedDatabaseName,
            _ => throw new ArgumentException($"'{serviceKey}' is not a known service.", nameof(serviceKey)),
        };

        var databases = await _databaseRepository.GetByTenantIdAsync(tenant.Id);
        if (databases.Any(d => string.Equals(d.ServiceName, serviceKey, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        await _databaseRepository.CreateAsync(new TenantServiceDatabase
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ServiceName = serviceKey,
            DatabaseName = databaseName,
            Status = "Provisioned",
            CreatedAtUtc = DateTime.UtcNow,
        });
        if (serviceKey == "Crm")
        {
            await SeedDefaultCrmStagesAsync(tenant.OrganizationId, cancellationToken);
        }
        return true;
    }

    public string BuildCrmConnectionString(string? serverAddress, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_crmSharedConnectionString) { InitialCatalog = databaseName };
        if (!string.IsNullOrWhiteSpace(serverAddress))
        {
            builder.DataSource = serverAddress;
        }
        return builder.ConnectionString;
    }

    public async Task<bool> CrmDatabaseExistsAsync(string? serverAddress, string databaseName, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(BuildCrmConnectionString(serverAddress, databaseName));
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    public async Task EnsureCrmSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var checkCommand = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Leads'", connection);
            var alreadyHasSchema = (int)(await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
            if (alreadyHasSchema)
            {
                return;
            }
        }

        // Bundled Content item (see crm-schema/README.md) -- copied next to this app's own
        // executable at publish time, so it's always found relative to AppContext.BaseDirectory
        // regardless of working directory.
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "crm-schema", "crm-full-schema.sql");
        var schemaScript = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        await _tenantDataService.ExecuteScriptAsync(connectionString, schemaScript, cancellationToken);
    }

    private async Task SeedDefaultCrmStagesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(BuildCrmConnectionString(null, SharedCrmDatabaseName));
        await connection.OpenAsync(cancellationToken);

        var sortOrder = 1;
        foreach (var stage in DefaultPipelineStages)
        {
            await using var command = new SqlCommand(
                "INSERT INTO PipelineStages (Id, Name, SortOrder, IsActive, OrganizationId, IsWon, IsLost, IsDefault) " +
                "VALUES (@Id, @Name, @SortOrder, 1, @OrganizationId, @IsWon, @IsLost, @IsDefault)",
                connection);
            command.Parameters.AddWithValue("@Id", Guid.NewGuid());
            command.Parameters.AddWithValue("@Name", stage.Name);
            command.Parameters.AddWithValue("@SortOrder", sortOrder++);
            command.Parameters.AddWithValue("@OrganizationId", organizationId);
            command.Parameters.AddWithValue("@IsWon", stage.IsWon);
            command.Parameters.AddWithValue("@IsLost", stage.IsLost);
            command.Parameters.AddWithValue("@IsDefault", stage.IsDefault);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        sortOrder = 1;
        foreach (var stage in DefaultProductStages)
        {
            await using var command = new SqlCommand(
                "INSERT INTO ProductStages (Id, OrganizationId, Name, SortOrder, IsActive, IsWon, IsLost, IsDefault) " +
                "VALUES (@Id, @OrganizationId, @Name, @SortOrder, 1, @IsWon, @IsLost, @IsDefault)",
                connection);
            command.Parameters.AddWithValue("@Id", Guid.NewGuid());
            command.Parameters.AddWithValue("@OrganizationId", organizationId);
            command.Parameters.AddWithValue("@Name", stage.Name);
            command.Parameters.AddWithValue("@SortOrder", sortOrder++);
            command.Parameters.AddWithValue("@IsWon", stage.IsWon);
            command.Parameters.AddWithValue("@IsLost", stage.IsLost);
            command.Parameters.AddWithValue("@IsDefault", stage.IsDefault);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
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
