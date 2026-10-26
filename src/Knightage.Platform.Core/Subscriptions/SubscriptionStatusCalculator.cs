namespace Knightage.Platform.Core.Subscriptions;

/// <summary>
/// Status is always computed from the subscription's dates, never stored --
/// no background job to transition it and nothing that can drift out of sync
/// with whatever a super user last set.
/// </summary>
public static class SubscriptionStatusCalculator
{
    public const string Active = "Active";
    public const string ReadOnly = "ReadOnly";
    public const string Disabled = "Disabled";
    public const string NotSubscribed = "NotSubscribed";

    /// <summary>
    /// A tenant with no subscription row at all (endDate is null) is unrestricted,
    /// same as Active -- this lets the feature ship without locking out every
    /// existing tenant that predates it. It's reported as a distinct status so
    /// "never configured" doesn't look identical to a real active subscription.
    /// </summary>
    public static string Compute(DateTime? endDate, int gracePeriodDays)
    {
        if (endDate is null)
        {
            return NotSubscribed;
        }

        var today = DateTime.UtcNow.Date;
        if (today <= endDate.Value.Date)
        {
            return Active;
        }

        if (today <= endDate.Value.Date.AddDays(gracePeriodDays))
        {
            return ReadOnly;
        }

        return Disabled;
    }
}
