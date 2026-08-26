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

    /// <summary>Set only when this Item was created through the full
    /// purchase-intake workflow (PurchaseService.CreateAsync) — null for
    /// Items created via the older quick-entry/import paths, which have no
    /// line concept.</summary>
    public Guid? PurchaseItemLineId { get; set; }
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

    // Descriptive fields collected at purchase-intake time — pure
    // pass-through data, never touched by allocation math.
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SkuCustomLabel { get; set; }
    public string? Condition { get; set; }
    public string? StorageLocation { get; set; }

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

    /// <summary>Used when a line's Quantity changes on Purchase update and
    /// surviving units get re-run through the allocator for the new count —
    /// never called for a reason other than "the line's own total split
    /// differently," so it's a distinct, explicit method rather than a
    /// public setter that could be mistaken for a manual correction (that's
    /// what CostBasisOverride is for).</summary>
    public void SetCalculatedCostBasis(decimal value) => CostBasisCalculated = value;
}
