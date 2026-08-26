namespace ResellerSystem.Domain.Shared.Dto;

public sealed class ReferenceListValueDto
{
    public required Guid Id { get; init; }
    public required string ListKey { get; init; }
    public required string Value { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsSystemDefault { get; init; }
}

public sealed class CreateReferenceListValueRequest
{
    public required string ListKey { get; init; }
    public required string Value { get; init; }
    public int SortOrder { get; init; }
}

public sealed class PurchaseItemLineDto
{
    public required Guid Id { get; init; }
    public required int LineNumber { get; init; }
    public required string ItemName { get; init; }
    public string? CategoryName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPurchaseCost { get; init; }
    public required decimal LinePurchaseCost { get; init; }
    public required decimal AllocatedSalesTax { get; init; }
    public decimal? ManualAllocatedSalesTax { get; init; }
    public required decimal AllocatedExpenses { get; init; }
    public decimal? ManualAllocatedExpenses { get; init; }
    public required decimal FinalLineCostBasis { get; init; }
    public string? Notes { get; init; }

    /// <summary>The physical Item numbers this line has created — empty
    /// for a not-yet-saved preview line.</summary>
    public IReadOnlyList<long> ItemNumbers { get; init; } = Array.Empty<long>();
}

/// <summary>Client → server shape for one line, used both when creating a
/// Purchase and when previewing allocation before saving. Id is null for a
/// new line; set (matching an existing PurchaseItemLine.Id) when editing —
/// that distinction is what lets UpdateAsync tell "update this line" from
/// "this is a new one" apart.</summary>
public sealed class PurchaseItemLineInput
{
    public Guid? Id { get; init; }
    public required string ItemName { get; init; }
    public string? CategoryName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPurchaseCost { get; init; }

    /// <summary>Only read when the Purchase's SalesTaxAllocationMethod/
    /// ExpenseAllocationMethod is "Manual" for the respective pool.</summary>
    public decimal? ManualAllocatedSalesTax { get; init; }
    public decimal? ManualAllocatedExpenses { get; init; }
    public string? Notes { get; init; }
}

public sealed class PurchaseExpenseLineDto
{
    public required Guid Id { get; init; }
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public string? Notes { get; init; }
}

public sealed class PurchaseExpenseLineInput
{
    public required string ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public string? Notes { get; init; }
}

/// <summary>The full purchase-intake shape — Product Specification
/// sections 3-19. Distinct from the older, minimal CreatePurchaseRequest
/// (single source/item/quantity), which stays untouched for backward
/// compatibility with the quick-entry form and Import.</summary>
public class CreatePurchaseFullRequest
{
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SourceType { get; init; }

    /// <summary>"TaxPaid" | "ResellerPermit" | "NoTax".</summary>
    public required string PurchaseType { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Comment { get; init; }

    // Reseller Permit block — only meaningful when PurchaseType == "ResellerPermit".
    public string? PermitNumber { get; init; }
    public DateOnly? PermitDate { get; init; }
    public decimal? TaxExemptAmount { get; init; }

    // Financial
    /// <summary>Defaults to the computed MerchandiseSubtotal when null.</summary>
    public decimal? TaxableAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
    /// <summary>Manually overrides the calculated Sales Tax Amount —
    /// sets Purchase.SalesTaxIsManualOverride and preserves the calculated
    /// value separately, same Source-vs-Calculated pattern as Item's cost
    /// basis.</summary>
    public decimal? SalesTaxAmountOverride { get; init; }
    public string SalesTaxAllocationMethod { get; init; } = "Proportional";
    public string ExpenseAllocationMethod { get; init; } = "Proportional";
    public decimal ManualAdjustment { get; init; }

    public required IReadOnlyList<PurchaseItemLineInput> ItemLines { get; init; }
    public IReadOnlyList<PurchaseExpenseLineInput> ExpenseLines { get; init; } = Array.Empty<PurchaseExpenseLineInput>();

    /// <summary>Strict Mode is the default (Product Specification §18):
    /// if Manual allocation leaves a Difference between the computed Total
    /// and the sum of manually-entered line allocations, the save is
    /// rejected unless this is explicitly true.</summary>
    public bool AllowDifference { get; init; }
}

public sealed class UpdatePurchaseFullRequest : CreatePurchaseFullRequest
{
}

public sealed class PurchaseDetailDto
{
    public required Guid Id { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SourceType { get; init; }
    public required string PurchaseType { get; init; }
    public required bool UsedResellerPermit { get; init; }
    public string? PermitNumber { get; init; }
    public DateOnly? PermitDate { get; init; }
    public decimal? TaxExemptAmount { get; init; }
    public string? PaymentMethod { get; init; }
    public string? Comment { get; init; }

    public required decimal MerchandiseSubtotal { get; init; }
    public required decimal TaxableAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
    /// <summary>Effective value (calculated, unless manually overridden).</summary>
    public required decimal SalesTaxAmount { get; init; }
    public decimal? SalesTaxAmountCalculated { get; init; }
    public required bool SalesTaxIsManualOverride { get; init; }
    public required string SalesTaxAllocationMethod { get; init; }
    public required string ExpenseAllocationMethod { get; init; }
    public required decimal ManualAdjustment { get; init; }
    /// <summary>Effective Total Purchase Cost.</summary>
    public required decimal TotalAmount { get; init; }

    public required IReadOnlyList<PurchaseItemLineDto> ItemLines { get; init; }
    public required IReadOnlyList<PurchaseExpenseLineDto> ExpenseLines { get; init; }

    public required int TotalItemCount { get; init; }
    public required int SoldItemCount { get; init; }
    public required int RemainingItemCount { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required string UpdatedBy { get; init; }
}

public sealed class PurchaseListRowDto
{
    public required Guid Id { get; init; }
    public required DateOnly PurchaseDate { get; init; }
    public required string SourceName { get; init; }
    public required string PurchaseType { get; init; }
    public required bool UsedResellerPermit { get; init; }
    public string? PaymentMethod { get; init; }
    public required decimal MerchandiseSubtotal { get; init; }
    public required decimal SalesTaxAmount { get; init; }
    public required decimal TotalExpenses { get; init; }
    public required decimal TotalAmount { get; init; }
    public required int ItemCount { get; init; }
    public required int RemainingItemCount { get; init; }
    public required int SoldItemCount { get; init; }
}

public sealed class PurchaseListFilterRequest
{
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
    public string? SourceName { get; init; }
    public string? PurchaseType { get; init; }
    public bool? UsedResellerPermit { get; init; }
    public string? PaymentMethod { get; init; }
    public decimal? MinTotalAmount { get; init; }
    public decimal? MaxTotalAmount { get; init; }
    public string? Search { get; init; }
}

/// <summary>Shared shape for both the read-only preview endpoint and the
/// real Create/Update path — same PurchaseAllocationCalculator computes
/// this either way, so client and server can never disagree on numbers.</summary>
public sealed class PurchaseAllocationPreviewRequest
{
    public decimal? TaxableAmount { get; init; }
    public decimal? SalesTaxRate { get; init; }
    public decimal? SalesTaxAmountOverride { get; init; }
    public string SalesTaxAllocationMethod { get; init; } = "Proportional";
    public string ExpenseAllocationMethod { get; init; } = "Proportional";
    public decimal ManualAdjustment { get; init; }
    public required IReadOnlyList<PurchaseItemLineInput> ItemLines { get; init; }
    public IReadOnlyList<PurchaseExpenseLineInput> ExpenseLines { get; init; } = Array.Empty<PurchaseExpenseLineInput>();
}

public sealed class PurchaseAllocationLineResultDto
{
    public Guid? Id { get; init; }
    public required int LineNumber { get; init; }
    public required string ItemName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPurchaseCost { get; init; }
    public required decimal LinePurchaseCost { get; init; }
    public required decimal AllocatedSalesTax { get; init; }
    public required decimal AllocatedExpenses { get; init; }
    public required decimal FinalLineCostBasis { get; init; }

    /// <summary>Per-physical-unit cost basis — length == Quantity, always
    /// summing exactly to FinalLineCostBasis (MoneyAllocator guarantee).</summary>
    public required IReadOnlyList<decimal> UnitCostBases { get; init; }
}

public sealed class PurchaseAllocationResult
{
    public required decimal MerchandiseSubtotal { get; init; }
    public required decimal TaxableAmount { get; init; }
    public required decimal SalesTaxAmount { get; init; }
    public required decimal TotalExpenses { get; init; }
    public required decimal ManualAdjustment { get; init; }
    public required decimal TotalPurchaseCost { get; init; }
    /// <summary>Sum of every line's FinalLineCostBasis.</summary>
    public required decimal AllocatedTotal { get; init; }
    public required decimal Difference { get; init; }
    public required int PhysicalItemsToCreate { get; init; }
    public required bool IsReadyToSave { get; init; }
    public required IReadOnlyList<PurchaseAllocationLineResultDto> Lines { get; init; }
    public required IReadOnlyList<string> ValidationErrors { get; init; }
}
