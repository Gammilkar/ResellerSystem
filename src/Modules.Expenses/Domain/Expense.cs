namespace ResellerSystem.Modules.Expenses.Domain;

public static class ExpenseTypes
{
    public const string ShippingLabel = "ShippingLabel";
    public const string ReturnShipping = "ReturnShipping";
    public const string Packaging = "Packaging";
    public const string Supplies = "Supplies";
    public const string Software = "Software";
    public const string Storage = "Storage";
    public const string Mileage = "Mileage";
    public const string Other = "Other";
}

public sealed class Expense
{
    public Guid Id { get; private set; }
    public string ExpenseType { get; set; } = ExpenseTypes.Other;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public Guid? PurchaseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? SaleId { get; set; }
    public Guid? ReturnId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    private Expense() { }

    public static Expense CreateNew(string expenseType, decimal amount, DateOnly expenseDate,
        Guid? purchaseId, Guid? itemId, Guid? saleId, Guid? returnId, string? paymentMethod, string? comment)
    {
        var now = DateTimeOffset.UtcNow;
        return new Expense
        {
            Id = Guid.NewGuid(),
            ExpenseType = expenseType,
            Amount = amount,
            ExpenseDate = expenseDate,
            PurchaseId = purchaseId,
            ItemId = itemId,
            SaleId = saleId,
            ReturnId = returnId,
            PaymentMethod = paymentMethod,
            Comment = comment,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
