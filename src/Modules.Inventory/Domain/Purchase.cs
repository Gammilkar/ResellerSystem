namespace ResellerSystem.Modules.Inventory.Domain;

public sealed class Purchase
{
    public Guid Id { get; private set; }
    public DateOnly PurchaseDate { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SalesTaxAmount { get; set; }
    public decimal? SalesTaxRate { get; set; }
    public string? PaymentMethod { get; set; }
    public bool UsedResellerPermit { get; set; }
    public string PurchaseType { get; set; } = "TaxPaid"; // "TaxPaid" | "ResellerPermit" | "NoTax" — Product Specification section 26
    public string? Comment { get; set; }

    // Full purchase-intake workflow (PurchaseItemLine/PurchaseExpenseLine) —
    // populated only when this Purchase was created through that path; the
    // old quick-entry CreatePurchaseAsync path leaves these at their
    // defaults (no lines, MerchandiseSubtotal/TaxableAmount = 0) and never
    // reads them. TotalAmount above is repurposed to mean "the effective
    // Total Purchase Cost" for BOTH paths — for quick-entry purchases it's
    // still just the raw entered total; for full-workflow purchases it's
    // always recomputed as MerchandiseSubtotal + SalesTaxAmount +
    // Σ(ExpenseLines.Amount) + ManualAdjustment.
    public decimal MerchandiseSubtotal { get; set; }
    public decimal TaxableAmount { get; set; }

    /// <summary>Source-vs-Calculated pair for SalesTaxAmount, same pattern
    /// as Item.CostBasisCalculated/CostBasisOverride — SalesTaxAmount
    /// itself always holds the effective value; this preserves what the
    /// allocator would have computed so a manual override is visible and
    /// reversible rather than silently overwriting history.</summary>
    public decimal? SalesTaxAmountCalculated { get; set; }
    public bool SalesTaxIsManualOverride { get; set; }

    /// <summary>"Proportional" (by line cost) | "EqualPerUnit" (by
    /// Quantity) | "Manual" (each line's own ManualAllocatedSalesTax/
    /// ManualAllocatedExpenses wins, no allocator run) — chosen once per
    /// Purchase, not per expense line, to avoid a many-to-many allocation
    /// table for a rarely-needed case.</summary>
    public string SalesTaxAllocationMethod { get; set; } = "Proportional";
    public string ExpenseAllocationMethod { get; set; } = "Proportional";

    /// <summary>Folded directly into the Total Purchase Cost formula
    /// (Product Specification's own formula lists it as its own line, not
    /// an "Other Expense") — the one, explicit correction mechanism; there
    /// is deliberately no second "override the whole total" field on top
    /// of this.</summary>
    public decimal ManualAdjustment { get; set; }

    // Reseller Permit block — only meaningful when PurchaseType ==
    // "ResellerPermit", but kept as plain nullable fields rather than a
    // sub-object since nothing else in this codebase's Purchase/Item shape
    // uses value-object sub-records for optional field groups.
    public string? PermitNumber { get; set; }
    public DateOnly? PermitDate { get; set; }
    public decimal? TaxExemptAmount { get; set; }

    public string? SourceType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    public List<Item> Items { get; private set; } = new();
    public List<PurchaseItemLine> ItemLines { get; private set; } = new();
    public List<PurchaseExpenseLine> ExpenseLines { get; private set; } = new();

    private Purchase() { } // EF Core

    public static Purchase CreateNew(DateOnly purchaseDate, string sourceName, decimal totalAmount,
        decimal salesTaxAmount, decimal? salesTaxRate, string? paymentMethod, string purchaseType, string? comment)
    {
        var now = DateTimeOffset.UtcNow;
        return new Purchase
        {
            Id = Guid.NewGuid(),
            PurchaseDate = purchaseDate,
            SourceName = sourceName.Trim(),
            TotalAmount = totalAmount,
            SalesTaxAmount = salesTaxAmount,
            SalesTaxRate = salesTaxRate,
            PaymentMethod = paymentMethod,
            PurchaseType = purchaseType,
            UsedResellerPermit = purchaseType == "ResellerPermit",
            Comment = comment,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
