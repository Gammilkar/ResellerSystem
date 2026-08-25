namespace ResellerSystem.Modules.Sales.Domain;

/// <summary>
/// KNOWN SCOPE SIMPLIFICATION (see KNOWN_LIMITATIONS.md "Sales module
/// scope"): ReturnShippingCost/OtherExpense arguably belong to a future
/// Expense entity linked by ReturnId, not embedded here — kept on Return
/// for now because the Expenses module doesn't exist yet, documented
/// rather than silently modeled as if it were the final design.
/// </summary>
public sealed class Return
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ItemId { get; private set; }
    public DateOnly ReturnDate { get; set; }
    public string ReturnType { get; set; } = "Full";
    public decimal RefundToBuyer { get; set; }
    public decimal RefundedShipping { get; set; }
    public decimal MarketplaceFeeCredit { get; set; }
    public decimal ReturnShippingCost { get; set; }
    public decimal OtherExpense { get; set; }
    public bool PhysicallyReturned { get; set; }
    public string? ConditionOnReturn { get; set; }
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";

    private Return() { }

    public static Return CreateNew(
        Guid saleId, Guid itemId, DateOnly returnDate, string returnType,
        decimal refundToBuyer, decimal refundedShipping, decimal marketplaceFeeCredit,
        decimal returnShippingCost, decimal otherExpense, bool physicallyReturned,
        string? conditionOnReturn, string? comment)
    {
        var now = DateTimeOffset.UtcNow;
        return new Return
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ItemId = itemId,
            ReturnDate = returnDate,
            ReturnType = returnType,
            RefundToBuyer = refundToBuyer,
            RefundedShipping = refundedShipping,
            MarketplaceFeeCredit = marketplaceFeeCredit,
            ReturnShippingCost = returnShippingCost,
            OtherExpense = otherExpense,
            PhysicallyReturned = physicallyReturned,
            ConditionOnReturn = conditionOnReturn,
            Comment = comment,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
