namespace ResellerSystem.Domain.Shared.Dto;

public sealed class SupplierDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateSupplierRequest
{
    public required string Name { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateSupplierRequest
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Notes { get; init; }
}

/// <summary>One row per Purchase made from a given supplier — the
/// "purchase history" list on the supplier's card.</summary>
public sealed class SupplierPurchaseHistoryRowDto
{
    public required Guid PurchaseId { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int ItemCount { get; init; }
}
