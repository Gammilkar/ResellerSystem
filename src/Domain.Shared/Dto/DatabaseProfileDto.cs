using ResellerSystem.Domain.Shared.Enums;

namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>
/// Public representation of a tenant database. Never exposes the physical
/// PostgreSQL database name — only the immutable public Id and the
/// user-editable display Name.
/// </summary>
public sealed class DatabaseProfileDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string TimeZone { get; init; }
    public required string Currency { get; init; }
    public required DatabaseStatusDto Status { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
