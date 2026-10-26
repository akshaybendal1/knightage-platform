namespace Knightage.Platform.Core;

public record ServiceCatalogEntry(string Key, string DisplayName);

// The first canonical list of business-app service names. ProvisioningService.cs already
// uses "Crm"/"Accounting" as string literals when auto-provisioning tenant databases, and
// reserves "InventorySales" in a comment for when that app gets real data-access code --
// this catalog keys off the same three strings so the two stay in sync.
public static class ServiceCatalog
{
    public static readonly IReadOnlyList<ServiceCatalogEntry> Entries = new[]
    {
        new ServiceCatalogEntry("Crm", "CRM"),
        new ServiceCatalogEntry("Accounting", "Accounting"),
        new ServiceCatalogEntry("InventorySales", "Inventory"),
    };
}
