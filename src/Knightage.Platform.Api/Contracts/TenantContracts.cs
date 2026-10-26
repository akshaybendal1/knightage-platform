using System.ComponentModel.DataAnnotations;

namespace Knightage.Platform.Api.Contracts;

public record ProvisionTenantRequest(
    [Required] Guid OrganizationId,
    [Required] string OrganizationName);

public record UpdateAccentColorRequest(
    [RegularExpression("^#[0-9A-Fa-f]{6}$")] string? AccentColor);

public record UpdateFieldForceTrackingRequest(bool Enabled);

public record UpdateAccountingMultiCompanyRequest(bool Enabled);

public record UpdateServiceStatusRequest(bool Enabled);

public record AssignPlanRequest(Guid? PlanId);

public record RequestPlanChangeRequest([Required] Guid PlanId);

// TargetServer null/blank means "the default shared server" (CrmShared's own server) --
// the common case of moving a tenant to another database on the server everyone's already
// on. The target database must already exist (this app has no CREATE DATABASE rights and,
// with sharding, "create a fresh one" isn't always even the right action) -- a PlatformAdmin
// provisions it, and grants this app's login access, out of band before calling this.
public record MoveCrmDataRequest(string? TargetServer, [Required] string TargetDatabaseName);
