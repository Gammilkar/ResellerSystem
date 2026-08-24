namespace ResellerSystem.Modules.Sales.Domain;

/// <summary>Marketplace fees ONLY — never a selling expense (shipping
/// label, packaging, return shipping). See migration header comment.</summary>
public static class SaleFeeTypes
{
    public const string FinalValueFee = "FinalValueFee";
    public const string PerOrderFee = "PerOrderFee";
    public const string InsertionFee = "InsertionFee";
    public const string ListingUpgradeFee = "ListingUpgradeFee";
    public const string PromotedAdFee = "PromotedAdFee";
    public const string InternationalFee = "InternationalFee";
    public const string DisputeFee = "DisputeFee";
    public const string FeeTax = "FeeTax";
    public const string FeeCredit = "FeeCredit"; // negative amount
    public const string Other = "Other";
}

public sealed class SaleFee
{
    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public string FeeType { get; set; } = SaleFeeTypes.Other;
    public decimal Amount { get; set; }
    public decimal? Rate { get; set; }
    public string Source { get; set; } = "manual"; // manual | imported | api
    public DateTimeOffset CreatedAt { get; private set; }

    private SaleFee() { }

    public static SaleFee CreateNew(Guid saleId, string feeType, decimal amount, decimal? rate, string source)
    {
        return new SaleFee
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            FeeType = feeType,
            Amount = amount,
            Rate = rate,
            Source = source,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
