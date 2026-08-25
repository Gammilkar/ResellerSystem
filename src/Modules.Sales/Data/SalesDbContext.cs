using Microsoft.EntityFrameworkCore;
using ResellerSystem.Modules.Sales.Domain;

namespace ResellerSystem.Modules.Sales.Data;

public sealed class SalesDbContext : DbContext
{
    public SalesDbContext(DbContextOptions<SalesDbContext> options) : base(options) { }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleFee> SaleFees => Set<SaleFee>();
    public DbSet<Return> Returns => Set<Return>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Listing>(entity =>
        {
            entity.ToTable("listings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Marketplace).HasColumnName("marketplace").IsRequired();
            entity.Property(e => e.MarketplaceAccount).HasColumnName("marketplace_account");
            entity.Property(e => e.ExternalListingId).HasColumnName("external_listing_id");
            entity.Property(e => e.PublishedDate).HasColumnName("published_date");
            entity.Property(e => e.ListingPrice).HasColumnName("listing_price");
            entity.Property(e => e.Promoted).HasColumnName("promoted");
            entity.Property(e => e.PromotedRate).HasColumnName("promoted_rate");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ListingId).HasColumnName("listing_id");
            entity.Property(e => e.Marketplace).HasColumnName("marketplace").IsRequired();
            entity.Property(e => e.MarketplaceAccount).HasColumnName("marketplace_account");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.TransactionId).HasColumnName("transaction_id");
            entity.Property(e => e.SaleDate).HasColumnName("sale_date");
            entity.Property(e => e.ItemSalePrice).HasColumnName("item_sale_price");
            entity.Property(e => e.BuyerPaidShipping).HasColumnName("buyer_paid_shipping");
            entity.Property(e => e.BuyerPaidSalesTax).HasColumnName("buyer_paid_sales_tax");
            entity.Property(e => e.Handling).HasColumnName("handling");
            entity.Property(e => e.SellerDiscount).HasColumnName("seller_discount");
            entity.Property(e => e.GrossTransactionAmount).HasColumnName("gross_transaction_amount");
            entity.Property(e => e.MarketplaceCollectedTax).HasColumnName("marketplace_collected_tax");
            entity.Property(e => e.PayoutAmount).HasColumnName("payout_amount");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method");
            entity.Property(e => e.DestinationState).HasColumnName("destination_state");
            entity.Property(e => e.DestinationZip).HasColumnName("destination_zip");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasMany(e => e.Fees).WithOne().HasForeignKey(f => f.SaleId);
        });

        modelBuilder.Entity<SaleFee>(entity =>
        {
            entity.ToTable("sale_fees");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.FeeType).HasColumnName("fee_type").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Rate).HasColumnName("rate");
            entity.Property(e => e.Source).HasColumnName("source");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Return>(entity =>
        {
            entity.ToTable("returns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ReturnDate).HasColumnName("return_date");
            entity.Property(e => e.ReturnType).HasColumnName("return_type");
            entity.Property(e => e.RefundToBuyer).HasColumnName("refund_to_buyer");
            entity.Property(e => e.RefundedShipping).HasColumnName("refunded_shipping");
            entity.Property(e => e.MarketplaceFeeCredit).HasColumnName("marketplace_fee_credit");
            entity.Property(e => e.ReturnShippingCost).HasColumnName("return_shipping_cost");
            entity.Property(e => e.OtherExpense).HasColumnName("other_expense");
            entity.Property(e => e.PhysicallyReturned).HasColumnName("physically_returned");
            entity.Property(e => e.ConditionOnReturn).HasColumnName("condition_on_return");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });
    }
}
