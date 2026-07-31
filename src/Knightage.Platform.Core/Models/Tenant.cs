namespace Knightage.Platform.Core.Models;

public class Tenant
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAtUtc { get; set; }
}
