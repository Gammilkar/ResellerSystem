namespace ResellerSystem.Modules.Inventory.Domain;

/// <summary>One additional purchase cost — Buyer Premium, Processing Fee,
/// Delivery, Other — modeled as a flexible list (Product Specification
/// section 36-37) instead of hardcoded columns, so new expense types don't
/// need a code change. No downstream FK references this row from anywhere
/// else, so (unlike PurchaseItemLine) a Purchase update simply replaces the
/// whole set rather than diffing it.</summary>
public sealed class PurchaseExpenseLine
{
    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";

    private PurchaseExpenseLine() { } // EF Core

    public static PurchaseExpenseLine CreateNew(Guid purchaseId, string expenseType, decimal amount, string? notes)
    {
        var now = DateTimeOffset.UtcNow;
        return new PurchaseExpenseLine
        {
            Id = Guid.NewGuid(),
            PurchaseId = purchaseId,
            ExpenseType = expenseType.Trim(),
            Amount = amount,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
