using Knightage.Platform.Core.Models;

namespace Knightage.Platform.Core.Interfaces;

public interface IPlanRepository
{
    Task<IReadOnlyList<Plan>> GetAllAsync(bool includeInactive = false);
    Task<Plan?> GetByIdAsync(Guid id);
    Task<Plan> CreateAsync(Plan plan);
    Task UpdateAsync(Plan plan);
    Task SetActiveAsync(Guid id, bool isActive);
}
