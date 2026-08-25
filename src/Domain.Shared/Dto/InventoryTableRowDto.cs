namespace ResellerSystem.Domain.Shared.Dto;

/// <summary>
/// Product Specification section 34 ("Таблица товаров") — one row per
/// Item, with its Purchase/latest-Listing/latest-Sale data flattened in.
/// Read-only, computed live; nothing here is stored under these names.
/// </summary>
public sealed class InventoryTableRowDto
{
    public required Guid ItemId { get; init; }
    public required long ItemNumber { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }

    public required Guid PurchaseId { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required string PurchaseSourceName { get; init; }
    public required string PurchaseType { get; init; }

    public required decimal CostBasis { get; init; }

    public Guid? ListingId { get; init; }
    public DateOnly? ListingPublishedDate { get; init; }
    public string? ListingMarketplace { get; init; }

    public Guid? SaleId { get; init; }
    public DateOnly? SaleDate { get; init; }
    public string? SaleMarketplace { get; init; }
    public decimal? SalePrice { get; init; }

    /// <summary>Days from first listed (or from purchase, if never listed)
    /// to sold — or to today, if still unsold and already listed. Null if
    /// unsold and never listed.</summary>
    public int? DaysListed { get; init; }
}
