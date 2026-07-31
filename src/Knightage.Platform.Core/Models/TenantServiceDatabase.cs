namespace Knightage.Platform.Core.Models;

public class TenantServiceDatabase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = "Provisioned";
    public DateTime CreatedAtUtc { get; set; }
}
