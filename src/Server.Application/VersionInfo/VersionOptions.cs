namespace ResellerSystem.Server.Application.VersionInfo;

/// <summary>Bound from configuration (appsettings) — see Server.Api "Versioning" section.</summary>
public sealed class VersionOptions
{
    public const string SectionName = "Versioning";

    public string ServerVersion { get; set; } = "0.1.0";
    public string ApiVersion { get; set; } = "1";
    public string MinimumDesktopClientVersion { get; set; } = "0.1.0";
    public string MinimumAndroidClientVersion { get; set; } = "0.1.0";
}
