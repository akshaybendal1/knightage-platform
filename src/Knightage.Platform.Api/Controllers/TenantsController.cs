using Knightage.Platform.Api.Contracts;
using Knightage.Platform.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly IProvisioningService _provisioningService;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantServiceDatabaseRepository _databaseRepository;

    public TenantsController(
        IProvisioningService provisioningService,
        ITenantRepository tenantRepository,
        ITenantServiceDatabaseRepository databaseRepository)
    {
        _provisioningService = provisioningService;
        _tenantRepository = tenantRepository;
        _databaseRepository = databaseRepository;
    }

    [HttpPost("provision")]
    public async Task<IActionResult> Provision(ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _provisioningService.ProvisionAsync(request.OrganizationId, request.OrganizationName, cancellationToken);
        return Ok(new { tenant = result.Tenant, databases = result.Databases });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _tenantRepository.GetAllAsync());

    [HttpGet("{organizationId:guid}")]
    public async Task<IActionResult> GetByOrganizationId(Guid organizationId)
    {
        var tenant = await _tenantRepository.GetByOrganizationIdAsync(organizationId);
        if (tenant is null)
        {
            return NotFound();
        }

        var databases = await _databaseRepository.GetByTenantIdAsync(tenant.Id);
        return Ok(new { tenant, databases });
    }
}
