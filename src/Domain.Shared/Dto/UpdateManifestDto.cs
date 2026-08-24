namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>Matches the update-manifest.json format from Product
/// Development Plan v1.0, Part 2.3 — published as a GitHub Release asset.</summary>
public sealed class UpdateManifestDto
{
    public required string ProductVersion { get; init; }
    public required DateTimeOffset ReleasedAt { get; init; }
    public required string MinimumUpgradeFromVersion { get; init; }
    public required UpdatePackageDto Server { get; init; }
    public string? ReleaseNotesUrl { get; init; }
}

public sealed class UpdatePackageDto
{
    public required string Url { get; init; }
    public required string Sha256 { get; init; }
    public required long SizeBytes { get; init; }
}

public sealed class UpdateCheckResultDto
{
    public required bool UpdateAvailable { get; init; }
    public required string CurrentVersion { get; init; }
    public string? AvailableVersion { get; init; }
    public string? ReleaseNotesUrl { get; init; }
}

public enum UpdateInstallStatusDto { NotStarted, Downloading, BackingUp, Applying, HealthChecking, Completed, RolledBack, Failed }

public sealed class UpdateInstallResultDto
{
    public required UpdateInstallStatusDto Status { get; init; }
    public string? Message { get; init; }
    public string? BackupId { get; init; }
}
