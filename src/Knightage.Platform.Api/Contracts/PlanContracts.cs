using System.ComponentModel.DataAnnotations;

namespace Knightage.Platform.Api.Contracts;

public record CreatePlanRequest(
    [Required, MaxLength(100)] string Name,
    [Range(1, int.MaxValue)] int? MaxUsers,
    [Range(1, int.MaxValue)] int? MaxRecords,
    [Range(0, double.MaxValue)] decimal? PriceMonthly,
    int SortOrder = 0);

public record UpdatePlanRequest(
    [Required, MaxLength(100)] string Name,
    [Range(1, int.MaxValue)] int? MaxUsers,
    [Range(1, int.MaxValue)] int? MaxRecords,
    [Range(0, double.MaxValue)] decimal? PriceMonthly,
    int SortOrder = 0);

public record SetPlanStatusRequest(bool IsActive);
