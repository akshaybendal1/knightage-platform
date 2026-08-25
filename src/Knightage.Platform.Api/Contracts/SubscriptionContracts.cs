using System.ComponentModel.DataAnnotations;

namespace Knightage.Platform.Api.Contracts;

public record UpdateSubscriptionRequest(
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    int GracePeriodDays = 7,
    string? Notes = null);
