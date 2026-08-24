using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Server.Application.VersionInfo;

/// <summary>
/// Single source of truth for all version numbers surfaced by the API.
/// Nothing else in the codebase should hardcode a version string.
/// </summary>
public interface IVersionProvider
{
    VersionResponse GetVersion();
    string ServerVersion { get; }
    int MasterSchemaVersion { get; }
    int CurrentTenantSchemaVersion { get; }
}
