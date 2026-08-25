using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>
/// The full field catalog for "xlsx-full" imports — Product Specification
/// sections 25/32/38/41/43/45/49 (one row = one Item, with its Purchase,
/// Listing, Sale, Fees, Return, and selling-Expenses all denormalized onto
/// that row). Keys are stable identifiers used in a batch's ColumnMapping
/// and in saved ImportMappingTemplates — never rename an existing key,
/// only add new ones, or saved templates break.
/// </summary>
public static class ImportTargetFields
{
    public static readonly IReadOnlyList<ImportTargetFieldDto> All = new List<ImportTargetFieldDto>
    {
        // Purchase
        new() { Key = "purchase.sourceName", DisplayName = "Purchase Source / Place", Group = "Purchase", Required = true },
        new() { Key = "purchase.date", DisplayName = "Purchase Date", Group = "Purchase", Required = true },
        new() { Key = "purchase.type", DisplayName = "Purchase Type (TaxPaid/ResellerPermit/NoTax)", Group = "Purchase" },
        new() { Key = "purchase.salesTaxAmount", DisplayName = "Sales Tax at Purchase", Group = "Purchase" },
        new() { Key = "purchase.salesTaxRate", DisplayName = "Tax Rate", Group = "Purchase" },
        new() { Key = "purchase.paymentMethod", DisplayName = "Purchase Payment Method", Group = "Purchase" },
        new() { Key = "purchase.additionalExpenses", DisplayName = "Additional Purchase Expenses", Group = "Purchase" },
        new() { Key = "purchase.comment", DisplayName = "Purchase Comment", Group = "Purchase" },
        new() { Key = "purchase.groupKey", DisplayName = "Purchase ID (groups rows into one Purchase)", Group = "Purchase" },

        // Item
        new() { Key = "item.name", DisplayName = "Item Name", Group = "Item", Required = true },
        new() { Key = "item.category", DisplayName = "Category", Group = "Item" },
        new() { Key = "item.status", DisplayName = "Status", Group = "Item" },
        new() { Key = "item.notes", DisplayName = "Notes", Group = "Item" },
        new() { Key = "item.purchasePrice", DisplayName = "Item Purchase Price", Group = "Item", Required = true },
        new() { Key = "item.costBasisOverride", DisplayName = "Cost Basis (override)", Group = "Item" },
        new() { Key = "item.quantity", DisplayName = "Quantity (splits into that many identical Items)", Group = "Item" },

        // Listing
        new() { Key = "listing.marketplace", DisplayName = "Marketplace (Listing)", Group = "Listing" },
        new() { Key = "listing.marketplaceAccount", DisplayName = "Marketplace Account (Listing)", Group = "Listing" },
        new() { Key = "listing.externalListingId", DisplayName = "External Listing ID", Group = "Listing" },
        new() { Key = "listing.publishedDate", DisplayName = "First Published Date", Group = "Listing" },
        new() { Key = "listing.listingPrice", DisplayName = "Listing Price", Group = "Listing" },
        new() { Key = "listing.url", DisplayName = "Listing URL", Group = "Listing" },
        new() { Key = "listing.promoted", DisplayName = "Promoted (yes/no)", Group = "Listing" },
        new() { Key = "listing.promotedRate", DisplayName = "Promoted Rate", Group = "Listing" },
        new() { Key = "listing.endDate", DisplayName = "Listing End Date", Group = "Listing" },

        // Sale
        new() { Key = "sale.marketplace", DisplayName = "Marketplace (Sale)", Group = "Sale" },
        new() { Key = "sale.marketplaceAccount", DisplayName = "Marketplace Account (Sale)", Group = "Sale" },
        new() { Key = "sale.orderId", DisplayName = "Order ID", Group = "Sale" },
        new() { Key = "sale.transactionId", DisplayName = "Transaction ID", Group = "Sale" },
        new() { Key = "sale.saleDate", DisplayName = "Sale Date", Group = "Sale" },
        new() { Key = "sale.itemSalePrice", DisplayName = "Sale Price", Group = "Sale" },
        new() { Key = "sale.buyerPaidShipping", DisplayName = "Shipping Paid by Buyer", Group = "Sale" },
        new() { Key = "sale.buyerPaidSalesTax", DisplayName = "Sales Tax Paid by Buyer", Group = "Sale" },
        new() { Key = "sale.marketplaceCollectedTax", DisplayName = "Marketplace Collected Tax", Group = "Sale" },
        new() { Key = "sale.payoutAmount", DisplayName = "Payout Amount", Group = "Sale" },
        new() { Key = "sale.destinationState", DisplayName = "Buyer State", Group = "Sale" },
        new() { Key = "sale.destinationZip", DisplayName = "Buyer ZIP", Group = "Sale" },
        new() { Key = "sale.paymentMethod", DisplayName = "Sale Payment Method / Account", Group = "Sale" },

        // Marketplace fees (each becomes one sale_fees row)
        new() { Key = "fee.finalValueFee", DisplayName = "Final Value Fee", Group = "Fees" },
        new() { Key = "fee.finalValueFeeRate", DisplayName = "Final Value Fee Rate", Group = "Fees" },
        new() { Key = "fee.perOrderFee", DisplayName = "Per-order Fee", Group = "Fees" },
        new() { Key = "fee.insertionFee", DisplayName = "Insertion Fee", Group = "Fees" },
        new() { Key = "fee.listingUpgradeFee", DisplayName = "Listing Upgrade Fee", Group = "Fees" },
        new() { Key = "fee.promotedListingsFee", DisplayName = "Promoted Listings Fee", Group = "Fees" },
        new() { Key = "fee.internationalFee", DisplayName = "International Fee", Group = "Fees" },
        new() { Key = "fee.taxOnSellerFees", DisplayName = "Tax on Seller Fees", Group = "Fees" },
        new() { Key = "fee.feeCredit", DisplayName = "Fee Credit", Group = "Fees" },
        new() { Key = "fee.disputeFee", DisplayName = "Dispute Fee", Group = "Fees" },
        new() { Key = "fee.chargebackFee", DisplayName = "Chargeback Fee", Group = "Fees" },
        new() { Key = "fee.otherMarketplaceFees", DisplayName = "Other Marketplace Fees", Group = "Fees" },

        // Selling expenses (each becomes one expenses row linked to the sale)
        new() { Key = "expense.shippingLabel", DisplayName = "Shipping Label Cost", Group = "Selling Expenses" },
        new() { Key = "expense.packaging", DisplayName = "Packaging Cost", Group = "Selling Expenses" },
        new() { Key = "expense.insurance", DisplayName = "Insurance Cost", Group = "Selling Expenses" },
        new() { Key = "expense.other", DisplayName = "Other Selling Expense", Group = "Selling Expenses" },

        // Return
        new() { Key = "return.type", DisplayName = "Return Type", Group = "Return" },
        new() { Key = "return.date", DisplayName = "Return Date", Group = "Return" },
        new() { Key = "return.refundAmount", DisplayName = "Refund Amount", Group = "Return" },
        new() { Key = "return.refundedShipping", DisplayName = "Refunded Shipping", Group = "Return" },
        new() { Key = "return.marketplaceFeeCredit", DisplayName = "Return Marketplace Fee Credit", Group = "Return" },
        new() { Key = "return.shippingCost", DisplayName = "Return Shipping Cost", Group = "Return" },
        new() { Key = "return.physicallyReturned", DisplayName = "Physically Returned (yes/no)", Group = "Return" },
        new() { Key = "return.conditionOnReturn", DisplayName = "Condition on Return", Group = "Return" },
        new() { Key = "return.statusAfterReturn", DisplayName = "Item Status After Return", Group = "Return" },
    };

    private static readonly HashSet<string> ValidKeys = All.Select(f => f.Key).ToHashSet();

    public static bool IsKnownKey(string key) => ValidKeys.Contains(key);
}
