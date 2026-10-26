namespace Knightage.Platform.Core.Models;

public class TenantServiceDatabase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    // Null means "the default/shared server for this service" (the only option before
    // tenant splitting/sharding across servers was supported) -- only set once a tenant's
    // data has been moved to a database on a different SQL Server.
    public string? ServerAddress { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = "Provisioned";
    public DateTime CreatedAtUtc { get; set; }
}
