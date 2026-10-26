namespace Knightage.Platform.Core.Models;

/// <summary>
/// Search, filter and sort options for the PlatformAdmin tenants grid. Every value is optional;
/// an empty query returns all tenants newest-first, which is what the grid showed before it
/// gained these controls.
/// </summary>
public class TenantListQuery
{
    public const string PlanNone = "none";

    /// <summary>Sort keys the grid may request. Anything else falls back to <see cref="SortCreated"/>.</summary>
    public const string SortName = "name";
    public const string SortSlug = "slug";
    public const string SortPlan = "plan";
    public const string SortSubscription = "subscription";
    public const string SortEnds = "ends";
    public const string SortCreated = "created";

    /// <summary>Matches tenant name, slug or organization id (substring, case-insensitive).</summary>
    public string? Search { get; init; }

    /// <summary>A plan id, or <see cref="PlanNone"/> for tenants with no plan assigned.</summary>
    public string? Plan { get; init; }

    /// <summary>One of the SubscriptionStatusCalculator statuses.</summary>
    public string? SubscriptionStatus { get; init; }

    public string SortBy { get; init; } = SortCreated;

    public bool Descending { get; init; } = true;
}
