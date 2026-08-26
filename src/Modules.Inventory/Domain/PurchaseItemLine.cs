namespace ResellerSystem.Modules.Inventory.Domain;

/// <summary>One distinct item type within a Purchase — e.g. "Vintage Book,
/// Quantity 5" — as entered by the user, before it explodes into that many
/// individual physical Item rows on save. Unlike PurchaseExpenseLine, this
/// is a first-class entity with real soft-delete: Item.PurchaseItemLineId
/// is a genuine FK, so an update that removed-and-reinserted lines would
/// orphan/break that traceability on the very first edit of any purchase.</summary>
public sealed class PurchaseItemLine
{
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public int LineNumber { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPurchaseCost { get; set; }

    /// <summary>Always Quantity × UnitPurchaseCost, recomputed server-side
    /// on every save — never trusted from the client directly.</summary>
    public decimal LinePurchaseCost { get; set; }

    public decimal AllocatedSalesTax { get; set; }
    /// <summary>Only meaningful when Purchase.SalesTaxAllocationMethod ==
    /// "Manual" — the allocator is skipped entirely and this value is used
    /// as-is for AllocatedSalesTax.</summary>
    public decimal? ManualAllocatedSalesTax { get; set; }

    public decimal AllocatedExpenses { get; set; }
    /// <summary>Same idea as ManualAllocatedSalesTax, for
    /// Purchase.ExpenseAllocationMethod == "Manual".</summary>
    public decimal? ManualAllocatedExpenses { get; set; }

    /// <summary>LinePurchaseCost + AllocatedSalesTax + AllocatedExpenses —
    /// what gets split (via MoneyAllocator, equal-weight) across this
    /// line's Quantity physical Items.</summary>
    public decimal FinalLineCostBasis { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    private PurchaseItemLine() { } // EF Core

    public static PurchaseItemLine CreateNew(Guid purchaseId, int lineNumber, string itemName, string? categoryName,
        int quantity, decimal unitPurchaseCost, string? notes)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseItemLine
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            LineNumber = lineNumber,
            ItemName = itemName.Trim(),
            CategoryName = categoryName,
            Quantity = quantity,
            UnitPurchaseCost = unitPurchaseCost,
            LinePurchaseCost = quantity * unitPurchaseCost,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
