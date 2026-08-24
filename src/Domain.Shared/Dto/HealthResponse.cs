namespace ResellerSystem.Domain.Shared.Dto;

public sealed class HealthResponse
{
    public required string Status { get; init; } // "healthy" | "degraded" | "unhealthy"
    public required string ServerVersion { get; init; }
    public required string MasterDatabase { get; init; } // "healthy" | "unhealthy"
    public required string FileStorage { get; init; }     // "healthy" | "unhealthy"
    public required long AvailableDiskSpaceBytes { get; init; }
    public required DateTimeOffset TimeUtc { get; init; }
    public required string Environment { get; init; }
}
