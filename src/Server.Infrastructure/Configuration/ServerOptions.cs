namespace ResellerSystem.Server.Infrastructure.Configuration;

/// <summary>General server/network settings.</summary>
public sealed class ServerOptions
{
    public const string SectionName = "Server";

    /// <summary>e.g. "http://localhost:5000" in dev, "http://0.0.0.0:5000" for LAN production.</summary>
    public string BindAddress { get; set; } = "http://localhost:5000";

    /// <summary>Allowed CORS origins for the (future) web client. Empty by default — native clients don't need CORS.</summary>
    public string[] AllowedCorsOrigins { get; set; } = Array.Empty<string>();
}
