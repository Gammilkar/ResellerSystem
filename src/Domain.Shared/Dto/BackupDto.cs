namespace ResellerSystem.Domain.Shared.Dto;

public enum BackupTypeDto { Database, Full }

public sealed class BackupDatabaseEntryDto
{
    public required string PhysicalDatabaseName { get; init; }
    public required bool IsMaster { get; init; }
}

public sealed class BackupManifestDto
{
    public required string Id { get; init; }
    public required BackupTypeDto Type { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string ServerVersionAtBackup { get; init; }
    public required IReadOnlyList<BackupDatabaseEntryDto> Databases { get; init; }
    public required long TotalSizeBytes { get; init; }
}

public sealed class CreateBackupRequest
{
    public required BackupTypeDto Type { get; init; }
}
