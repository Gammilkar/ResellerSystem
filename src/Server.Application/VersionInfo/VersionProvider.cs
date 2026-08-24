using Microsoft.Extensions.Options;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Application.VersionInfo;

public sealed class VersionProvider : IVersionProvider
{
    private readonly VersionOptions _options;

    public VersionProvider(IOptions<VersionOptions> options)
    {
        _options = options.Value;
    }

    public string ServerVersion => _options.ServerVersion;
    public int MasterSchemaVersion => SchemaVersions.MasterCurrentVersion;
    public int CurrentTenantSchemaVersion => SchemaVersions.TenantCurrentVersion;

    public VersionResponse GetVersion() => new()
    {
        ServerVersion = _options.ServerVersion,
        ApiVersion = _options.ApiVersion,
        TenantSchemaVersion = CurrentTenantSchemaVersion,
        MasterSchemaVersion = MasterSchemaVersion,
        MinimumDesktopClientVersion = _options.MinimumDesktopClientVersion,
        MinimumAndroidClientVersion = _options.MinimumAndroidClientVersion
    };
}
