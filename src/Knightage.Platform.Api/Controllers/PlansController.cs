using Knightage.Platform.Api.Contracts;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

// Pricing tiers -- PlatformAdmin configures these, then assigns one to a tenant via
// TenantsController.UpdatePlan. No billing/payment integration yet; this is purely the
// limits/pricing-display side.
[Authorize(Roles = "PlatformAdmin")]
[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanRepository _planRepository;

    public PlansController(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _planRepository.GetAllAsync(includeInactive: true));

    [HttpPost]
    public async Task<IActionResult> Create(CreatePlanRequest request)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            MaxUsers = request.MaxUsers,
            MaxRecords = request.MaxRecords,
            PriceMonthly = request.PriceMonthly,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _planRepository.CreateAsync(plan);
        return Ok(plan);
    }

    // POST, not PUT -- the production host blocks PUT/DELETE at the server level (same
    // constraint as TenantsController.UpdateAccentColor/UpdateFieldForceTracking).
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePlanRequest request)
    {
        var existing = await _planRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return NotFound(new { message = "No plan with this id." });
        }

        existing.Name = request.Name;
        existing.MaxUsers = request.MaxUsers;
        existing.MaxRecords = request.MaxRecords;
        existing.PriceMonthly = request.PriceMonthly;
        existing.SortOrder = request.SortOrder;
        await _planRepository.UpdateAsync(existing);
        return Ok(existing);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetPlanStatusRequest request)
    {
        var existing = await _planRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return NotFound(new { message = "No plan with this id." });
        }

        await _planRepository.SetActiveAsync(id, request.IsActive);
        return NoContent();
    }
}
