namespace Knightage.Platform.Core.Access;

/// <summary>The four things a user can be allowed to do in an area of an app.</summary>
public static class AccessActions
{
    public const string View = "view";
    public const string Add = "add";
    public const string Edit = "edit";
    public const string Delete = "delete";

    public static readonly IReadOnlyList<string> All = new[] { View, Add, Edit, Delete };
}

/// <param name="Label">What the screen calls the action -- "Cancel" for orders and vouchers, "Deactivate" where nothing is really deleted.</param>
public record AccessAction(string Key, string Label);

/// <param name="Key">The app's own key for the area (the same one its server checks and its menu uses).</param>
/// <param name="AdminDefault">What an Admin may do here when nothing has been customised: every listed action, or none.</param>
public record AccessFeature(
    string Key, string Label, string Group, IReadOnlyList<AccessAction> Actions,
    bool AdminDefault, bool MemberDefault, bool SalesDefault)
{
    public bool Has(string action) => Actions.Any(a => a.Key == action);

    public bool DefaultFor(string role) => role switch
    {
        AccessCatalog.Owner => true,
        AccessCatalog.Admin => AdminDefault,
        AccessCatalog.Member => MemberDefault,
        AccessCatalog.Sales => SalesDefault,
        _ => false,
    };
}

/// <param name="Service">The ServiceCatalog key ("Crm", "InventorySales", "Accounting").</param>
public record AccessApp(string Service, string DisplayName, IReadOnlyList<AccessFeature> Features)
{
    public AccessFeature? Find(string key) => Features.FirstOrDefault(f => f.Key == key);
}

/// <summary>
/// Every app and area whose access can be set per user on the Users screen, with what each role gets
/// by default. The defaults are what each app allowed before per-user access existed: in Inventory and
/// Accounting every role could do everything; in CRM a role could do everything on the screens it could
/// open (the Access Control defaults, which an organization may have changed -- CRM's own settings are
/// read for that, these are the fallback).
///
/// Each app keeps the same keys in its own PermissionCatalog (that is what it enforces) -- keep the copies
/// in step; tests/cross-app/user_access_check.py compares them.
///
/// Stored keys: "{Service}" for opening the app at all, "{Service}:{feature}:{action}" inside it.
/// </summary>
public static class AccessCatalog
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";
    public const string Sales = "Sales";

    /// <summary>Roles whose access can be changed. The Owner always has everything.</summary>
    public static readonly IReadOnlyList<string> ConfigurableRoles = new[] { Admin, Member, Sales };

    private static readonly AccessAction View = new(AccessActions.View, "View");
    private static readonly AccessAction Add = new(AccessActions.Add, "Add");
    private static readonly AccessAction Edit = new(AccessActions.Edit, "Edit");
    private static readonly AccessAction Delete = new(AccessActions.Delete, "Delete");
    private static readonly AccessAction Deactivate = new(AccessActions.Delete, "Deactivate");
    private static readonly AccessAction Cancel = new(AccessActions.Delete, "Cancel");

    private static AccessAction[] Only(params AccessAction[] actions) => actions;
    private static readonly AccessAction[] ViewOnly = { View };
    private static readonly AccessAction[] Full = { View, Add, Edit, Delete };

    private static AccessFeature Everyone(string key, string label, string group, AccessAction[] actions) =>
        new(key, label, group, actions, true, true, true);

    public static readonly IReadOnlyList<AccessApp> Apps = new[]
    {
        new AccessApp("Crm", "CRM", new[]
        {
            // Same keys and defaults as knightage-crm's ScreenCatalog (Admin, Member, Sales).
            new AccessFeature("dashboard", "Dashboard", "Everyday work", ViewOnly, true, true, false),
            new AccessFeature("leads", "Leads", "Everyday work", Full, true, true, false),
            new AccessFeature("customers", "Customers", "Everyday work", Only(View, Add), true, true, false),
            new AccessFeature("pipeline-stages", "Pipeline and product stages", "Everyday work", Full, true, true, false),
            new AccessFeature("timeline", "Timeline", "Everyday work", ViewOnly, true, true, false),
            new AccessFeature("tasks", "Tasks", "Everyday work", Only(View, Add, new AccessAction(AccessActions.Edit, "Complete")), true, true, false),
            new AccessFeature("calendar", "Calendar and meetings", "Everyday work", Only(View, new AccessAction(AccessActions.Add, "Schedule"), Edit), true, true, false),
            new AccessFeature("contact-us", "Contact us", "Everyday work", ViewOnly, true, true, false),
            new AccessFeature("sales-entry", "Sales entry", "Field sales", Only(View, Add), true, true, true),
            new AccessFeature("sales-dashboard", "Sales dashboard", "Field sales", ViewOnly, true, true, true),
            new AccessFeature("sales-coverage", "Sales coverage", "Field sales", ViewOnly, true, false, false),
            new AccessFeature("products", "Products", "Catalog and insights", Only(View, Add, Edit, Deactivate), true, false, false),
            new AccessFeature("product-categories", "Product categories", "Catalog and insights", Full, true, false, false),
            new AccessFeature("reports", "Reports", "Catalog and insights", ViewOnly, true, false, false),
            new AccessFeature("message-templates", "Message templates", "Catalog and insights", Only(View, Add, Edit, Deactivate), true, false, false),
        }),
        new AccessApp("InventorySales", "Inventory", new[]
        {
            Everyone("dashboard", "Dashboard", "Overview", ViewOnly),
            Everyone("products", "Products", "Catalog", Only(View, Add, Edit, Deactivate)),
            Everyone("product-categories", "Product categories", "Catalog", Full),
            Everyone("customers", "Customers", "People", Only(View, Add, Edit, Deactivate)),
            Everyone("suppliers", "Suppliers", "People", Only(View, Add, Edit, Deactivate)),
            Everyone("sales-orders", "Sales orders", "Orders", Only(View, Add, Cancel)),
            Everyone("purchase-orders", "Purchase orders", "Orders", Only(View, Add, Cancel)),
            Everyone("reports", "Reports", "Overview", ViewOnly),
        }),
        new AccessApp("Accounting", "Accounting", new[]
        {
            Everyone("vouchers", "Vouchers", "Daily work", Only(View, Add, Edit, Cancel)),
            Everyone("documents", "Documents and bank statements", "Daily work", Only(View, new AccessAction(AccessActions.Add, "Upload"), new AccessAction(AccessActions.Edit, "Post and match"), Delete)),
            Everyone("ledgers", "Ledgers", "Daily work", Only(View, Add, Edit, Deactivate)),
            Everyone("gst", "GST returns", "GST and reports", Only(View, new AccessAction(AccessActions.Add, "Import GSTR-2B"), new AccessAction(AccessActions.Edit, "Accept and ignore"), new AccessAction(AccessActions.Delete, "Delete imports"))),
            Everyone("statements", "Financial statements", "GST and reports", ViewOnly),
            Everyone("reports", "Reports", "GST and reports", ViewOnly),
            Everyone("companies", "Companies", "Setup", Only(View, Add, Edit, Deactivate)),
            Everyone("financial-years", "Financial years", "Setup", Only(View, Add, new AccessAction(AccessActions.Edit, "Close and reopen"))),
            Everyone("narration-rules", "Narration rules", "Setup", Full),
        }),
    };

    public static AccessApp? FindApp(string? service) =>
        Apps.FirstOrDefault(a => string.Equals(a.Service, service, StringComparison.OrdinalIgnoreCase));

    public static bool IsConfigurableRole(string? role) => role is not null && ConfigurableRoles.Contains(role, StringComparer.Ordinal);

    public static string AppKey(string service) => service;

    public static string ActionKey(string service, string feature, string action) => $"{service}:{feature}:{action}";

    /// <summary>
    /// True when the key names an app ("Crm") or one of its listed area actions ("Crm:leads:add"), spelt
    /// exactly as the catalog does. Anything else is refused rather than stored.
    /// </summary>
    public static bool IsValidKey(string key)
    {
        var parts = key.Split(':');
        var app = Apps.FirstOrDefault(a => a.Service == parts[0]);
        if (app is null)
        {
            return false;
        }
        return parts.Length switch
        {
            1 => true,
            3 => app.Find(parts[1])?.Has(parts[2]) == true,
            _ => false,
        };
    }
}
