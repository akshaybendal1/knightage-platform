using Knightage.DeployUtility;

if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
{
    PrintUsage();
    return args.Length == 0 ? 1 : 0;
}

var configPath = args[0];
var autoConfirm = args.Contains("--yes");
var dryRun = args.Contains("--dry-run");
var skipDb = args.Contains("--skip-db");

DeploymentConfig config;
try
{
    config = DeploymentConfig.Load(configPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Config error: {ex.Message}");
    return 1;
}

Console.WriteLine($"Deploying '{config.AppName}' from {config.LocalPath}");
if (dryRun) Console.WriteLine("(dry run -- no FTP or database calls will be made)");

var workDir = Path.GetFullPath(config.WorkDir);
var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
var backupDir = Path.Combine(workDir, "backups", timestamp);

try
{
    Console.WriteLine("\n[1/7] Zipping local deployment files");
    var zipPath = ZipService.ZipDirectory(config.LocalPath, workDir, config.AppName);
    Console.WriteLine($"  Created {zipPath}");

    Console.WriteLine("\n[2/7] Extracting build locally (prepped for upload; lets --dry-run validate without a connection)");
    var extractedDir = ZipService.ExtractZip(zipPath, workDir);
    Console.WriteLine($"  Extracted to {extractedDir}");

    if (dryRun)
    {
        Console.WriteLine("\nDry run complete. Nothing was uploaded, backed up, or run against the database.");
        return 0;
    }

    if (!autoConfirm && !Confirm($"About to deploy to {config.Ftp.Host}{config.Ftp.RemotePath}. Continue?"))
    {
        Console.WriteLine("Aborted.");
        return 1;
    }

    await using var ftp = await FtpDeployer.ConnectAsync(config.Ftp);

    Console.WriteLine("\n[3/7] Uploading zip to FTP location");
    await ftp.UploadZipAsync(zipPath);

    Console.WriteLine("\n[4/7] Backing up configs at the FTP location");
    await ftp.BackupConfigsAsync(config.ConfigFilesToPreserve, backupDir);

    Console.WriteLine("\n[5/7] Deploying unzipped build to FTP location");
    await ftp.DeployExtractedAsync(extractedDir);

    Console.WriteLine("\n[6/7] Replacing backed-up config");
    await ftp.RestoreConfigsAsync(config.ConfigFilesToPreserve, backupDir);

    if (skipDb || config.DbScripts.Count == 0)
    {
        Console.WriteLine("\n[7/7] Skipping DB scripts" + (skipDb ? " (--skip-db)" : " (none configured)"));
    }
    else
    {
        Console.WriteLine($"\n[7/7] About to run {config.DbScripts.Count} DB script(s) against the PRODUCTION database '{config.Database!.Database}'.");
        if (!autoConfirm && !Confirm("This step is not reversible. Continue?"))
        {
            Console.WriteLine("Skipped DB scripts. App files are already deployed -- run this tool again once you're ready, or apply the scripts by hand.");
            return 1;
        }

        await DbScriptRunner.RunScriptsAsync(config.Database!, config.DbScripts);
    }

    Console.WriteLine($"\nDone. '{config.AppName}' deployed successfully.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\nDeployment failed: {ex.Message}");
    Console.Error.WriteLine($"Local backups (if any config was backed up before the failure) are at: {backupDir}");
    return 1;
}

static bool Confirm(string prompt)
{
    Console.Write($"{prompt} [y/N] ");
    var answer = Console.ReadLine();
    return string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
}

static void PrintUsage()
{
    Console.WriteLine("""
        Knightage Deploy Utility

        Usage:
          knightage-deploy <config.json> [--yes] [--dry-run] [--skip-db]

        Options:
          --yes       Skip confirmation prompts (for scripted/CI use).
          --dry-run   Zip and extract locally only; no FTP or database calls.
          --skip-db   Deploy app files but skip the DB script step.

        See deploy.config.example.json for the config file format.
        """);
}
