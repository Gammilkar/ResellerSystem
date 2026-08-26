namespace ResellerSystem.Domain.Shared.Dto;

public sealed class PurchaseDto
{
    public required Guid Id { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public Guid? SupplierId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal SalesTaxAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
    public string? PaymentMethod { get; init; }
    public required bool UsedResellerPermit { get; init; }
    public required string PurchaseType { get; init; }
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

    /// <summary>"TaxPaid" | "ResellerPermit" | "NoTax" — Product
    /// Specification section 26. UsedResellerPermit is derived from this.</summary>
    public string PurchaseType { get; init; } = "TaxPaid";
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
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? SkuCustomLabel { get; init; }
    public string? Condition { get; init; }
    public string? StorageLocation { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Creates a Purchase with no items yet — used by Import to
/// group several spreadsheet rows (sharing the same source Purchase ID
/// column) under one Purchase, adding items one at a time via
/// AddItemToPurchaseRequest as each row is processed. The manual "New
/// Purchase" screen still uses CreatePurchaseRequest, which always seeds
/// the first item(s) immediately.</summary>
public sealed class CreatePurchaseHeaderRequest
{
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public required decimal TotalAmount { get; init; }
    public decimal SalesTaxAmount { get; init; } = 0;
    public decimal? SalesTaxRate { get; init; }
    public string? PaymentMethod { get; init; }
    public string PurchaseType { get; init; } = "TaxPaid";
    public string? Comment { get; init; }
}

public sealed class AddItemToPurchaseRequest
{
    public required string Name { get; init; }
    public string? CategoryName { get; init; }
    public required decimal CostBasis { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateItemRequest
{
    public string? Name { get; init; }
    public string? CategoryName { get; init; }
    public string? Status { get; init; }
    public decimal? CostBasisOverride { get; init; }
    public string? Notes { get; init; }
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public string? SerialNumber { get; init; }
    public string? SkuCustomLabel { get; init; }
    public string? Condition { get; init; }
    public string? StorageLocation { get; init; }
}

public sealed class UpdatePurchaseRequest
{
    public string? SourceName { get; init; }
    public Guid? SupplierId { get; init; }
    public string? PurchaseType { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public bool? UsedResellerPermit { get; init; }
    public decimal? SalesTaxAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
}
