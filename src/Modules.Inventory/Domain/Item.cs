namespace ResellerSystem.Modules.Inventory.Domain;

/// <summary>Starting system statuses — Architecture Plan v0.1 section 12.
/// Stored as plain text (not a DB enum) so custom statuses can be added
/// later without a migration, matching the "constructor" principle even
/// though the full editable-reference-table UI isn't built yet.</summary>
public static class ItemStatuses
{
    public const string Purchased = "Purchased";
    public const string InStock = "InStock";
    public const string NotListed = "NotListed";
    public const string Listed = "Listed";
    public const string Sold = "Sold";
    public const string Returned = "Returned";
    public const string Relisted = "Relisted";
    public const string WrittenOff = "WrittenOff";
    public const string Lost = "Lost";
    public const string PersonalUse = "PersonalUse";
}

public sealed class Item
{
    public Guid Id { get; private set; }
    public long ItemNumber { get; private set; }
    public Guid PurchaseId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string Status { get; set; } = ItemStatuses.Purchased;

    /// <summary>Auto-calculated as an even split of the parent Purchase's
    /// TotalAmount across all its items at creation time. See
    /// CostBasisOverride for the Source-vs-Calculated pattern
    /// (Architecture Plan v0.1, section 61).</summary>
    public decimal CostBasisCalculated { get; private set; }
    public decimal? CostBasisOverride { get; set; }
    public decimal EffectiveCostBasis => CostBasisOverride ?? CostBasisCalculated;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    public Purchase? Purchase { get; private set; }

    private Item() { } // EF Core

    public static Item CreateNew(Guid purchaseId, string name, string? categoryName, decimal costBasisCalculated, string? notes)
    {
        var now = DateTimeOffset.UtcNow;
        return new Item
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            Name = name.Trim(),
            CategoryName = categoryName,
            Status = ItemStatuses.Purchased,
            CostBasisCalculated = costBasisCalculated,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
