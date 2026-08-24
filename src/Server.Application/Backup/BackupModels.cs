namespace ResellerSystem.Server.Application.Backup;

public enum BackupType { Database, Full }

/// <summary>
/// Metadata for one backup, persisted as a sidecar JSON file next to the
/// actual dump files under StorageOptions.BackupRoot (not a database row —
/// a backup must be readable/listable even if the master database itself
/// is what needs restoring, so it deliberately does not depend on being
/// able to query Postgres).
/// </summary>
public sealed class BackupManifest
{
    public required string Id { get; init; }
    public required BackupType Type { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string ServerVersionAtBackup { get; init; }
    public required IReadOnlyList<BackupDatabaseEntry> Databases { get; init; }
    public string? StorageArchiveFileName { get; init; } // set only for Full backups
    public required long TotalSizeBytes { get; init; }
}

public sealed class BackupDatabaseEntry
{
    public required string PhysicalDatabaseName { get; init; }
    public required string DumpFileName { get; init; }
    public required string Sha256Checksum { get; init; }
    public required bool IsMaster { get; init; }
}
