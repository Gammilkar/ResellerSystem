namespace ResellerSystem.Server.FileStorage;

/// <summary>
/// Infrastructure for the four local root folders required by the
/// architecture. Stage 1 does not yet store real documents (that arrives
/// with Purchase/Item/Document entities), but the roots must exist, be
/// writable, and be reported through /health from day one.
/// </summary>
public interface IFileStorageService
{
    string StorageRoot { get; }
    string BackupRoot { get; }
    string UpdateRoot { get; }
    string TempRoot { get; }

    /// <summary>Creates any missing root folders.</summary>
    void EnsureRootsExist();

    /// <summary>Verifies the process can read and write under StorageRoot.</summary>
    Task<bool> CheckReadWriteAsync(CancellationToken ct = default);

    /// <summary>Free space, in bytes, on the volume hosting StorageRoot.</summary>
    long GetAvailableDiskSpaceBytes();
}
