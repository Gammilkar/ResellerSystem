namespace ResellerSystem.Domain.Shared.Dto;

public sealed class PurchaseDto
{
    public required Guid Id { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal SalesTaxAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
    public string? PaymentMethod { get; init; }
    public required bool UsedResellerPermit { get; init; }
    public string? Comment { get; init; }
    public required int ItemCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreatePurchaseRequest
{
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public required decimal TotalAmount { get; init; }
    public decimal SalesTaxAmount { get; init; } = 0;
    public decimal? SalesTaxRate { get; init; }
    public string? PaymentMethod { get; init; }
    public bool UsedResellerPermit { get; init; } = false;
    public string? Comment { get; init; }

    /// <summary>How many identical Item rows to create immediately —
    /// Architecture Plan v0.1 section 10: "если куплено 10 одинаковых
    /// товаров — создаются 10 отдельных Item". Cost basis is split evenly.</summary>
    public required string ItemName { get; init; }
    public int Quantity { get; init; } = 1;
    public string? CategoryName { get; init; }
}

public sealed class ItemDto
{
    public required Guid Id { get; init; }
    public required long ItemNumber { get; init; }
    public required Guid PurchaseId { get; init; }
    public required string Name { get; init; }
    public string? CategoryName { get; init; }
    public required string Status { get; init; }
    public required decimal CostBasisCalculated { get; init; }
    public decimal? CostBasisOverride { get; init; }
    public required decimal EffectiveCostBasis { get; init; }
    public string? Notes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class UpdateItemRequest
{
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public string? Status { get; init; }
    public decimal? CostBasisOverride { get; init; }
    public string? Notes { get; init; }
}
