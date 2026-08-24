namespace ResellerSystem.Domain.Shared.Dto;

public sealed class CreateDatabaseRequest
{
    public required string Name { get; init; }
    public required string TimeZone { get; init; }
    public string Currency { get; init; } = "USD";
}
