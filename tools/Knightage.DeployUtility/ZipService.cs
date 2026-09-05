using System.IO.Compression;

namespace Knightage.DeployUtility;

internal static class ZipService
{
    public static string ZipDirectory(string sourceDir, string workDir, string appName)
    {
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, $"{appName}-deploy.zip");
        if (File.Exists(zipPath)) File.Delete(zipPath);

        ZipFile.CreateFromDirectory(sourceDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        return zipPath;
    }

    public static string ExtractZip(string zipPath, string workDir)
    {
        var extractDir = Path.Combine(workDir, "extracted");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
        Directory.CreateDirectory(extractDir);

        ZipFile.ExtractToDirectory(zipPath, extractDir);
        return extractDir;
    }
}
