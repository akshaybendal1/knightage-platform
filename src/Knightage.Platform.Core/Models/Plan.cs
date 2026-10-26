namespace Knightage.Platform.Core.Models;

// A pricing tier -- MaxUsers/MaxRecords null means unlimited (used for a future
// uncapped/"Contact us" tier). PriceMonthly is display-only for now; plans are
// PlatformAdmin-assigned, there is no billing/payment integration yet.
public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MaxUsers { get; set; }
    public int? MaxRecords { get; set; }
    public decimal? PriceMonthly { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
