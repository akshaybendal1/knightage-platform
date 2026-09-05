using FluentFTP;

namespace Knightage.DeployUtility;

/// <summary>
/// Every step here is plain FTP (upload/download/rename/delete) -- no remote
/// shell/exec is assumed, since production FTP servers generally don't offer
/// one. "Unzipping remotely" is therefore done by extracting the zip locally
/// and mirror-uploading the extracted tree, not by any server-side unzip.
/// </summary>
internal sealed class FtpDeployer : IAsyncDisposable
{
    private const string BackupSuffix = ".bak";

    private readonly AsyncFtpClient _client;
    private readonly string _remoteRoot;

    private FtpDeployer(AsyncFtpClient client, string remoteRoot)
    {
        _client = client;
        _remoteRoot = remoteRoot.TrimEnd('/');
    }

    public static async Task<FtpDeployer> ConnectAsync(FtpSettings settings)
    {
        var client = new AsyncFtpClient(settings.Host, settings.Username, settings.Password, settings.Port);
        if (settings.UseSsl)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        }

        await client.Connect();
        return new FtpDeployer(client, settings.RemotePath);
    }

    private string RemotePath(string relative) => $"{_remoteRoot}/{relative.TrimStart('/')}";

    public async Task<string> UploadZipAsync(string localZipPath)
    {
        var remoteZipPath = RemotePath($"_incoming/{Path.GetFileName(localZipPath)}");
        Console.WriteLine($"  Uploading {Path.GetFileName(localZipPath)} -> {remoteZipPath}");
        await _client.UploadFile(localZipPath, remoteZipPath, FtpRemoteExists.Overwrite, createRemoteDir: true);
        return remoteZipPath;
    }

    /// <summary>
    /// Backs up each preserved config file both to a local timestamped folder
    /// (survives even if the remote deploy dies mid-flight) and to a rotating
    /// in-place ".bak" on the server (used by <see cref="RestoreConfigsAsync"/>).
    /// </summary>
    public async Task BackupConfigsAsync(IReadOnlyList<string> relativeConfigPaths, string localBackupDir)
    {
        Directory.CreateDirectory(localBackupDir);

        foreach (var relativePath in relativeConfigPaths)
        {
            var remotePath = RemotePath(relativePath);
            if (!await _client.FileExists(remotePath))
            {
                Console.WriteLine($"  No existing {relativePath} on server yet -- nothing to back up.");
                continue;
            }

            var localCopy = Path.Combine(localBackupDir, relativePath.Replace('/', '_'));
            Console.WriteLine($"  Backing up {relativePath} -> {localCopy}");
            await _client.DownloadFile(localCopy, remotePath, FtpLocalExists.Overwrite);

            var backupRemotePath = remotePath + BackupSuffix;
            if (await _client.FileExists(backupRemotePath))
            {
                await _client.DeleteFile(backupRemotePath);
            }
            await _client.Rename(remotePath, backupRemotePath);
        }
    }

    /// <summary>Mirror-uploads the extracted build to the site root. Never deletes remote files that aren't part of the build (logs, uploads, etc).</summary>
    public async Task DeployExtractedAsync(string extractedDir)
    {
        Console.WriteLine($"  Uploading build contents -> {_remoteRoot}");
        await _client.UploadDirectory(
            extractedDir,
            _remoteRoot,
            FtpFolderSyncMode.Update,
            FtpRemoteExists.Overwrite);
    }

    /// <summary>
    /// Puts the pre-deploy config back in place, overwriting whatever the
    /// fresh build shipped as its own default. Falls back to the local backup
    /// copy if the remote ".bak" is missing for any reason.
    /// </summary>
    public async Task RestoreConfigsAsync(IReadOnlyList<string> relativeConfigPaths, string localBackupDir)
    {
        foreach (var relativePath in relativeConfigPaths)
        {
            var remotePath = RemotePath(relativePath);
            var backupRemotePath = remotePath + BackupSuffix;

            if (await _client.FileExists(backupRemotePath))
            {
                Console.WriteLine($"  Restoring {relativePath} from server-side backup.");
                if (await _client.FileExists(remotePath))
                {
                    await _client.DeleteFile(remotePath);
                }
                await _client.Rename(backupRemotePath, remotePath);
                continue;
            }

            var localCopy = Path.Combine(localBackupDir, relativePath.Replace('/', '_'));
            if (File.Exists(localCopy))
            {
                Console.WriteLine($"  Server-side backup missing for {relativePath}; restoring from local copy instead.");
                await _client.UploadFile(localCopy, remotePath, FtpRemoteExists.Overwrite);
                continue;
            }

            Console.WriteLine($"  No backup found for {relativePath} -- leaving the freshly deployed build's own copy in place.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            await _client.Disconnect();
        }
        _client.Dispose();
    }
}
