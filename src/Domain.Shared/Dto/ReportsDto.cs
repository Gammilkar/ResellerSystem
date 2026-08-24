namespace ResellerSystem.Domain.Shared.Dto;

public sealed class MarketplaceProfitabilityRowDto
{
    public required string Marketplace { get; init; }
    public required int SaleCount { get; init; }
    public required decimal GrossSales { get; init; }
    public required decimal TotalFees { get; init; }
    public required decimal TotalCostBasis { get; init; }
    public required decimal NetProfit { get; init; }
    public decimal? RoiPercent { get; init; }
}

public sealed class CategoryProfitabilityRowDto
{
    public required string Category { get; init; }
    public required int PurchasedCount { get; init; }
    public required int SoldCount { get; init; }
    public required decimal AveragePurchasePrice { get; init; }
    public decimal? AverageSalePrice { get; init; }
    public required decimal NetProfit { get; init; }
    public decimal? RoiPercent { get; init; }
}

public sealed class InventoryAgingRowDto
{
    public required string Bucket { get; init; } // "0-30", "31-60", "61-90", "91-180", "180+"
    public required int ItemCount { get; init; }
    public required decimal TotalCostBasis { get; init; }
}

public sealed class FederalTaxSummaryDto
{
    public required int Year { get; init; }
    public required decimal GrossSales { get; init; }
    public required decimal MarketplaceFees { get; init; }
    public required decimal CostOfGoodsSold { get; init; }
    public required decimal NetProfit { get; init; }
}

/// <summary>Internal reconciliation aid — NOT an official IRS/marketplace
/// document. See Architecture Plan v0.1 section 28.</summary>
public sealed class Form1099KSummaryDto
{
    public required int Year { get; init; }
    public required string Marketplace { get; init; }
    public required decimal Box1aGrossPaymentAmount { get; init; }
    public required int Box3TransactionCount { get; init; }
}
