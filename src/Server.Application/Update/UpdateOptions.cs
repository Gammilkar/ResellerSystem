namespace ResellerSystem.Server.Application.Update;

public sealed class UpdateOptions
{
    public const string SectionName = "Updates";

    /// <summary>URL to update-manifest.json — e.g. a GitHub Releases asset
    /// URL: https://github.com/{owner}/{repo}/releases/latest/download/update-manifest.json
    /// (GitHub always resolves "latest" to the newest published release,
    /// so this URL never changes between releases).</summary>
    public string ManifestUrl { get; set; } = string.Empty;

    /// <summary>Seconds to wait for /health to report healthy after
    /// restarting on the new version before triggering rollback.</summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 120;
}
