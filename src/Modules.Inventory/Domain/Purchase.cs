namespace ResellerSystem.Modules.Inventory.Domain;

public sealed class Purchase
{
    public Guid Id { get; private set; }
    public DateOnly PurchaseDate { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal SalesTaxAmount { get; set; }
    public decimal? SalesTaxRate { get; set; }
    public string? PaymentMethod { get; set; }
    public bool UsedResellerPermit { get; set; }
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    public List<Item> Items { get; private set; } = new();

    private Purchase() { } // EF Core

    public static Purchase CreateNew(DateOnly purchaseDate, string sourceName, decimal totalAmount,
        decimal salesTaxAmount, decimal? salesTaxRate, string? paymentMethod, bool usedResellerPermit, string? comment)
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
            UsedResellerPermit = usedResellerPermit,
            Comment = comment,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;
}
