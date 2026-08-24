namespace ResellerSystem.Server.FileStorage;

public sealed class LocalFileStorageService : IFileStorageService
{
    public string StorageRoot { get; }
    public string BackupRoot { get; }
    public string UpdateRoot { get; }
    public string TempRoot { get; }

    public LocalFileStorageService(string storageRoot, string backupRoot, string updateRoot, string tempRoot)
    {
        StorageRoot = Path.GetFullPath(storageRoot);
        BackupRoot = Path.GetFullPath(backupRoot);
        UpdateRoot = Path.GetFullPath(updateRoot);
        TempRoot = Path.GetFullPath(tempRoot);
    }

    public void EnsureRootsExist()
    {
        foreach (var root in new[] { StorageRoot, BackupRoot, UpdateRoot, TempRoot })
        {
            Directory.CreateDirectory(root);
        }
    }

    public async Task<bool> CheckReadWriteAsync(CancellationToken ct = default)
    {
        try
        {
            var probeFile = Path.Combine(TempRoot, $".healthcheck-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probeFile, "ok", ct);
            var content = await File.ReadAllTextAsync(probeFile, ct);
            File.Delete(probeFile);
            return content == "ok";
        }
        catch
        {
            return false;
        }
    }

    public long GetAvailableDiskSpaceBytes()
    {
        try
        {
            var root = Path.GetPathRoot(StorageRoot);
            if (string.IsNullOrEmpty(root)) return 0;
            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }
}
