namespace ResellerSystem.Modules.Sales.Domain;

/// <summary>
/// Gross and Payout are deliberately separate fields — see the module's
/// migration header and the architecture fix that introduced this:
/// GrossTransactionAmount is buyer-facing revenue reporting,
/// PayoutAmount is what the marketplace actually deposited. Never derive
/// one from the other implicitly.
/// </summary>
public sealed class Sale
{
    public Guid Id { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? ListingId { get; private set; }
    public string Marketplace { get; set; } = string.Empty;
    public string? MarketplaceAccount { get; set; }
    public string? OrderId { get; set; }
    public string? TransactionId { get; set; }
    public DateOnly SaleDate { get; set; }

    public decimal ItemSalePrice { get; set; }
    public decimal BuyerPaidShipping { get; set; }
    public decimal BuyerPaidSalesTax { get; set; }
    public decimal Handling { get; set; }
    public decimal SellerDiscount { get; set; }
    public decimal GrossTransactionAmount { get; set; }
    public decimal MarketplaceCollectedTax { get; set; }
    public decimal PayoutAmount { get; set; }

    public int Quantity { get; set; } = 1;
    public string? PaymentMethod { get; set; }
    public string? DestinationState { get; set; }
    public string? DestinationZip { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
    public DateTimeOffset? DeletedAt { get; set; }

    public List<SaleFee> Fees { get; private set; } = new();

    private Sale() { }

    public static Sale CreateNew(
        Guid itemId, Guid? listingId, string marketplace, string? marketplaceAccount,
        string? orderId, string? transactionId, DateOnly saleDate,
        decimal itemSalePrice, decimal buyerPaidShipping, decimal buyerPaidSalesTax,
        decimal handling, decimal sellerDiscount, decimal marketplaceCollectedTax,
        decimal payoutAmount, int quantity, string? paymentMethod)
    {
        var now = DateTimeOffset.UtcNow;

        // GrossTransactionAmount is a calculated field at creation time —
        // stored (not computed on read) so it can later be overridden per
        // the Source-vs-Calculated principle without recomputation risk if
        // buyer-side components are edited independently afterward.
        var gross = itemSalePrice + buyerPaidShipping + buyerPaidSalesTax + handling - sellerDiscount;

        return new Sale
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            ListingId = listingId,
            Marketplace = marketplace.Trim(),
            MarketplaceAccount = marketplaceAccount,
            OrderId = orderId,
            TransactionId = transactionId,
            SaleDate = saleDate,
            ItemSalePrice = itemSalePrice,
            BuyerPaidShipping = buyerPaidShipping,
            BuyerPaidSalesTax = buyerPaidSalesTax,
            Handling = handling,
            SellerDiscount = sellerDiscount,
            GrossTransactionAmount = gross,
            MarketplaceCollectedTax = marketplaceCollectedTax,
            PayoutAmount = payoutAmount,
            Quantity = quantity,
            PaymentMethod = paymentMethod,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
