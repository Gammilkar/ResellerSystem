namespace ResellerSystem.Domain.Shared.Dto;

public sealed class ListingDto
{
    public required Guid Id { get; init; }
    public required Guid ItemId { get; init; }
    public required string Marketplace { get; init; }
    public string? MarketplaceAccount { get; init; }
    public string? ExternalListingId { get; init; }
    public DateOnly? PublishedDate { get; init; }
    public decimal? ListingPrice { get; init; }
    public required bool Promoted { get; init; }
    public decimal? PromotedRate { get; init; }
    public required string Status { get; init; }
    public string? Url { get; init; }
    public DateOnly? EndDate { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateListingRequest
{
    public required Guid ItemId { get; init; }
    public required string Marketplace { get; init; }
    public string? MarketplaceAccount { get; init; }
    public string? ExternalListingId { get; init; }
    public DateOnly? PublishedDate { get; init; }
    public decimal? ListingPrice { get; init; }
    public bool Promoted { get; init; }
    public decimal? PromotedRate { get; init; }
    public string? Url { get; init; }
    public DateOnly? EndDate { get; init; }
}

public sealed class UpdateListingRequest
{
    public string? Marketplace { get; init; }
    public DateOnly? PublishedDate { get; init; }
    public decimal? ListingPrice { get; init; }
}

public sealed class SaleFeeDto
{
    public required Guid Id { get; init; }
    public required string FeeType { get; init; }
    public required decimal Amount { get; init; }
    public decimal? Rate { get; init; }
}

public sealed class CreateSaleFeeRequest
{
    public required string FeeType { get; init; }
    public required decimal Amount { get; init; }
    public decimal? Rate { get; init; }
}

public sealed class SaleDto
{
    public required Guid Id { get; init; }
    public required Guid ItemId { get; init; }
    public Guid? ListingId { get; init; }
    public required string Marketplace { get; init; }
    public string? OrderId { get; init; }
    public string? TransactionId { get; init; }
    public required DateOnly SaleDate { get; init; }
    public required decimal ItemSalePrice { get; init; }
    public required decimal BuyerPaidShipping { get; init; }
    public required decimal BuyerPaidSalesTax { get; init; }
    public required decimal Handling { get; init; }
    public required decimal SellerDiscount { get; init; }
    public required decimal GrossTransactionAmount { get; init; }
    public required decimal MarketplaceCollectedTax { get; init; }
    public required decimal PayoutAmount { get; init; }
    public required int Quantity { get; init; }
    public string? DestinationState { get; init; }
    public string? DestinationZip { get; init; }
    public required IReadOnlyList<SaleFeeDto> Fees { get; init; }
}

public sealed class CreateSaleRequest
{
    public required Guid ItemId { get; init; }
    public Guid? ListingId { get; init; }
    public required string Marketplace { get; init; }
    public string? MarketplaceAccount { get; init; }
    public string? OrderId { get; init; }
    public string? TransactionId { get; init; }
    public required DateOnly SaleDate { get; init; }
    public required decimal ItemSalePrice { get; init; }
    public decimal BuyerPaidShipping { get; init; } = 0;
    public decimal BuyerPaidSalesTax { get; init; } = 0;
    public decimal Handling { get; init; } = 0;
    public decimal SellerDiscount { get; init; } = 0;
    public decimal MarketplaceCollectedTax { get; init; } = 0;
    public required decimal PayoutAmount { get; init; }
    public int Quantity { get; init; } = 1;
    public string? PaymentMethod { get; init; }
    public string? DestinationState { get; init; }
    public string? DestinationZip { get; init; }
}

public sealed class UpdateSaleRequest
{
    public DateOnly? SaleDate { get; init; }
    public string? Marketplace { get; init; }
    public decimal? ItemSalePrice { get; init; }
    public decimal? PayoutAmount { get; init; }
    public string? DestinationState { get; init; }
    public string? DestinationZip { get; init; }
}

/// <summary>Net Proceeds / Net Profit / ROI — Architecture Plan v0.1
/// sections 21-23, simplified (see KNOWN_LIMITATIONS.md "Sales module
/// scope"): does not yet factor in Returns.</summary>
public sealed class SaleFinancialsDto
{
    public required Guid SaleId { get; init; }
    public required decimal SellerGrossRevenue { get; init; }
    public required decimal TotalMarketplaceFees { get; init; }
    public required decimal NetProceeds { get; init; }
    public decimal? ItemCostBasis { get; init; }
    public decimal? NetProfit { get; init; }
    public decimal? RoiPercent { get; init; }
}

public sealed class CreateReturnRequest
{
    public required Guid SaleId { get; init; }
    public required Guid ItemId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public string ReturnType { get; init; } = "Full";
    public required decimal RefundToBuyer { get; init; }
    public decimal RefundedShipping { get; init; } = 0;
    public decimal MarketplaceFeeCredit { get; init; } = 0;
    public decimal ReturnShippingCost { get; init; } = 0;
    public decimal OtherExpense { get; init; } = 0;
    public required bool PhysicallyReturned { get; init; }
    public string? ConditionOnReturn { get; init; }
    public string? Comment { get; init; }
}

public sealed class ReturnDto
{
    public required Guid Id { get; init; }
    public required Guid SaleId { get; init; }
    public required Guid ItemId { get; init; }
    public required DateOnly ReturnDate { get; init; }
    public required string ReturnType { get; init; }
    public required decimal RefundToBuyer { get; init; }
    public required decimal RefundedShipping { get; init; }
    public required decimal MarketplaceFeeCredit { get; init; }
    public required decimal ReturnShippingCost { get; init; }
    public required decimal OtherExpense { get; init; }
    public required bool PhysicallyReturned { get; init; }
    public string? ConditionOnReturn { get; init; }
    public string? Comment { get; init; }
}
