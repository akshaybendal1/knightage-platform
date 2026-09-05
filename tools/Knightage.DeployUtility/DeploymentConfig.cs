using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knightage.DeployUtility;

/// <summary>
/// One config file = one app's deployment target. AppName is what shows up in
/// console output; it has no behavioral effect, it just keeps multiple configs
/// (platform, crm, accounting, ...) from being confused with each other.
/// </summary>
public sealed class DeploymentConfig
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "";

    /// <summary>Local folder to deploy, e.g. a `dotnet publish` output directory.</summary>
    [JsonPropertyName("localPath")]
    public string LocalPath { get; set; } = "";

    [JsonPropertyName("ftp")]
    public FtpSettings Ftp { get; set; } = new();

    /// <summary>
    /// Paths (relative to the site root on the FTP server) of files that hold
    /// real production values and must survive a deploy -- e.g. web.config,
    /// appsettings.Production.json. Left empty if this app has nothing to
    /// preserve (all config comes from IIS App Pool env vars).
    /// </summary>
    [JsonPropertyName("configFilesToPreserve")]
    public List<string> ConfigFilesToPreserve { get; set; } = new();

    /// <summary>SQL scripts to run against the production database, in order.</summary>
    [JsonPropertyName("dbScripts")]
    public List<string> DbScripts { get; set; } = new();

    [JsonPropertyName("database")]
    public DatabaseSettings? Database { get; set; }

    /// <summary>Scratch folder for zips/extracts/backups. Safe to delete between runs.</summary>
    [JsonPropertyName("workDir")]
    public string WorkDir { get; set; } = "./.deploy-work";

    public static DeploymentConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config file not found: {path}");
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<DeploymentConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException($"Config file is empty or invalid: {path}");

        config.Validate();
        return config;
    }

    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AppName)) errors.Add("appName is required.");
        if (string.IsNullOrWhiteSpace(LocalPath)) errors.Add("localPath is required.");
        else if (!Directory.Exists(LocalPath)) errors.Add($"localPath does not exist: {LocalPath}");

        if (string.IsNullOrWhiteSpace(Ftp.Host)) errors.Add("ftp.host is required.");
        if (string.IsNullOrWhiteSpace(Ftp.Username)) errors.Add("ftp.username is required.");
        if (string.IsNullOrWhiteSpace(Ftp.RemotePath)) errors.Add("ftp.remotePath is required.");

        if (DbScripts.Count > 0 && Database is null)
        {
            errors.Add("dbScripts is set but no database connection is configured.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException("Invalid deployment config:\n  - " + string.Join("\n  - ", errors));
        }
    }
}

public sealed class FtpSettings
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 21;

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("useSsl")]
    public bool UseSsl { get; set; } = true;

    /// <summary>Site root on the FTP server, e.g. "/site/knightage-platform".</summary>
    [JsonPropertyName("remotePath")]
    public string RemotePath { get; set; } = "";
}

public sealed class DatabaseSettings
{
    [JsonPropertyName("server")]
    public string Server { get; set; } = "";

    [JsonPropertyName("database")]
    public string Database { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("trustServerCertificate")]
    public bool TrustServerCertificate { get; set; } = true;

    public string ToConnectionString() =>
        $"Server={Server};Database={Database};User Id={UserId};Password={Password};TrustServerCertificate={TrustServerCertificate}";
}
