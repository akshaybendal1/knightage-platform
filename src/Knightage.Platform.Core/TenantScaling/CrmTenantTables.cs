namespace Knightage.Platform.Core.TenantScaling;

/// <summary>
/// Every OrganizationId-scoped table in knightage-crm's shared database, in FK-safe
/// order (parents before children) -- confirmed against every sql/0*.sql migration.
/// Import/copy uses this order; purge/delete uses the exact reverse. One list, reused
/// by every operation in CrmTenantDataService so there's a single source of truth.
/// </summary>
public static class CrmTenantTables
{
    public static readonly IReadOnlyList<string> InOrder = new[]
    {
        "PipelineStages",
        "Products",
        "ProductStages",
        "ProductCategories",
        "MessageTemplates",
        "OrganizationBranding",
        "SavedCustomReports",
        "Leads",
        "LeadActivities",
        "Tasks",
        "Meetings",
        "LeadProducts",
        "Documents",
        "DocumentLineItems",
    };
}
