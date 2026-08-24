namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>
/// Partial update. Only non-null fields are applied (PATCH semantics).
/// Physical database identity is never part of this contract.
/// </summary>
public sealed class UpdateDatabaseRequest
{
    public string? Name { get; init; }
    public string? TimeZone { get; init; }
    public bool? IsActive { get; init; }
}
