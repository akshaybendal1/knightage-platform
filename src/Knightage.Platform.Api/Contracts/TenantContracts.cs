using System.ComponentModel.DataAnnotations;

namespace Knightage.Platform.Api.Contracts;

public record ProvisionTenantRequest(
    [Required] Guid OrganizationId,
    [Required] string OrganizationName);
