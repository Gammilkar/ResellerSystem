namespace ResellerSystem.Server.FileStorage;

/// <summary>Root folders for local file storage. See Server.FileStorage.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string StorageRoot { get; set; } = "./data/storage";
    public string BackupRoot { get; set; } = "./data/backups";
    public string UpdateRoot { get; set; } = "./data/updates";
    public string TempRoot { get; set; } = "./data/temp";

    /// <summary>Warn (via /health) when free disk space drops below this many bytes.</summary>
    public long LowDiskSpaceWarningBytes { get; set; } = 20L * 1024 * 1024 * 1024; // 20 GB
}
