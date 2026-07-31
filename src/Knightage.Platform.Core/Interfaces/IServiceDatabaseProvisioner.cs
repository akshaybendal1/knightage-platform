namespace Knightage.Platform.Core.Interfaces;

/// <summary>
/// Creates a real, isolated SQL Server database for one business service and applies that
/// service's bootstrap schema to it. Implemented in Infrastructure since it talks to SQL Server
/// directly (CREATE DATABASE can't run inside the caller's own connection/transaction).
/// </summary>
public interface IServiceDatabaseProvisioner
{
    Task ProvisionAsync(string databaseName, string serviceName, CancellationToken cancellationToken = default);
}
