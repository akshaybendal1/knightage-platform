using Knightage.Platform.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Knightage.Platform.Infrastructure.Provisioning;

/// <summary>
/// Creates a real per-tenant SQL Server database on the same server as this service's own
/// database and applies the requesting business service's bootstrap schema to it. Schema files
/// are copies of each service's own sql/001_init.sql, bundled under the Api project's
/// schemas/ folder (see that project's README for why they're duplicated rather than shared).
/// </summary>
public class SqlServiceDatabaseProvisioner : IServiceDatabaseProvisioner
{
    private static readonly Dictionary<string, string> SchemaFilesByService = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Accounting"] = "accounting.sql",
        ["Crm"] = "crm.sql",
        ["InventorySales"] = "inventorysales.sql",
    };

    private readonly string _serverConnectionString;
    private readonly string _schemasDirectory;

    public SqlServiceDatabaseProvisioner(IConfiguration configuration)
    {
        _serverConnectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _schemasDirectory = Path.Combine(AppContext.BaseDirectory, "schemas");
    }

    public async Task ProvisionAsync(string databaseName, string serviceName, CancellationToken cancellationToken = default)
    {
        if (!SchemaFilesByService.TryGetValue(serviceName, out var schemaFile))
        {
            throw new InvalidOperationException($"No schema is registered for service '{serviceName}'.");
        }

        var schemaPath = Path.Combine(_schemasDirectory, schemaFile);
        var schemaSql = await File.ReadAllTextAsync(schemaPath, cancellationToken);

        var masterBuilder = new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = "master" };
        await using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
        {
            await masterConnection.OpenAsync(cancellationToken);
            if (!await DatabaseExistsAsync(masterConnection, databaseName, cancellationToken))
            {
                var createCommand = masterConnection.CreateCommand();
                // Database names come from this service's own slug generation, not user input,
                // so string interpolation here isn't attacker-controlled -- CREATE DATABASE
                // doesn't support parameterized identifiers anyway.
                createCommand.CommandText = $"CREATE DATABASE [{databaseName}]";
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var targetBuilder = new SqlConnectionStringBuilder(_serverConnectionString) { InitialCatalog = databaseName };
        await using var targetConnection = new SqlConnection(targetBuilder.ConnectionString);
        await targetConnection.OpenAsync(cancellationToken);
        var schemaCommand = targetConnection.CreateCommand();
        schemaCommand.CommandText = schemaSql;
        await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> DatabaseExistsAsync(SqlConnection connection, string databaseName, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sys.databases WHERE name = @Name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@Name";
        parameter.Value = databaseName;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }
}
