namespace Knightage.Platform.Core.Models;

public static class TenantMigrationStatus
{
    public const string AwaitingPurge = "AwaitingPurge";
    public const string Purged = "Purged";
}

public class TenantMigration
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? OldServerAddress { get; set; }
    public string OldDatabaseName { get; set; } = string.Empty;
    public string? NewServerAddress { get; set; }
    public string NewDatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = TenantMigrationStatus.AwaitingPurge;
    public string? BackupFilePath { get; set; }
    public DateTime MigratedAtUtc { get; set; }
    public DateTime? PurgedAtUtc { get; set; }
}
