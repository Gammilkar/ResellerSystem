namespace ResellerSystem.Domain.Shared.Dto;

public sealed class VersionResponse
{
    public required string ServerVersion { get; init; }
    public required string ApiVersion { get; init; }
    public required int TenantSchemaVersion { get; init; }
    public required int MasterSchemaVersion { get; init; }
    public required string MinimumDesktopClientVersion { get; init; }
    public required string MinimumAndroidClientVersion { get; init; }
}
