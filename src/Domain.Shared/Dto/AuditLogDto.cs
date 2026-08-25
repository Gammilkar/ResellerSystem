namespace ResellerSystem.Domain.Shared.Dto;

public sealed class AuditLogEntryDto
{
    public required Guid Id { get; init; }
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public required string Action { get; init; }
    public string? FieldName { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required DateTimeOffset ChangedAt { get; init; }
    public required string ChangedBy { get; init; }
    public required string Source { get; init; }
}
