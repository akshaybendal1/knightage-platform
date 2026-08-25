using Knightage.Platform.Api.Contracts;
using Knightage.Platform.Core.Interfaces;
using Knightage.Platform.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

// Only a PlatformAdmin manages vendor subscriptions -- this is billing-adjacent
// data about every customer, not something any single org's own users should see
// or touch about themselves or anyone else.
[Authorize(Roles = "PlatformAdmin")]
[ApiController]
[Route("api/vendors")]
public class SubscriptionsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;

    public SubscriptionsController(ITenantRepository tenantRepository, ISubscriptionRepository subscriptionRepository)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
    }

    // POST, not PUT -- the production host blocks PUT/DELETE at the server level.
    [HttpPost("{organizationId:guid}/subscription")]
    public async Task<IActionResult> UpdateSubscription(Guid organizationId, UpdateSubscriptionRequest request)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound(new { message = "No tenant is provisioned for this organization yet." });
        }

        if (request.EndDate.Date < request.StartDate.Date)
        {
            return BadRequest(new { message = "End date must be on or after the start date." });
        }

        var subscription = await _subscriptionRepository.UpsertAsync(new Subscription
        {
            TenantId = tenant.Id,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            GracePeriodDays = request.GracePeriodDays,
            Notes = request.Notes,
        });

        return Ok(subscription);
    }
}
