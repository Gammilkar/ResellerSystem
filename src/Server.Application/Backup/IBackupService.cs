namespace ResellerSystem.Server.Application.Backup;

/// <summary>
/// Core Backup Engine. Used both directly (Server Manager "Backup"/
/// "Restore" buttons -> /api/v1/backups) and internally by the Update
/// Engine, which requires a mandatory backup before every update install
/// (Product Development Plan v1.0, Part 2.3).
/// </summary>
public interface IBackupService
{
    /// <summary>Dumps every registered tenant database + the master
    /// database via pg_dump. Does NOT include file storage (documents).</summary>
    Task<BackupManifest> CreateDatabaseBackupAsync(CancellationToken ct = default);

    /// <summary>Database backup + a zip of the file storage root
    /// (documents) + a copy of the server's config folder.</summary>
    Task<BackupManifest> CreateFullBackupAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BackupManifest>> ListBackupsAsync(CancellationToken ct = default);

    Task<BackupManifest> GetManifestAsync(string backupId, CancellationToken ct = default);

    /// <summary>
    /// Restores every database dump in the given backup via pg_restore
    /// (DROP + recreate each physical database first), and — for Full
    /// backups — extracts the storage archive back over StorageRoot.
    /// Does NOT stop/start the Windows Service; callers (Server Manager,
    /// Update Engine rollback) are responsible for that around this call.
    /// </summary>
    Task RestoreAsync(string backupId, CancellationToken ct = default);
}
