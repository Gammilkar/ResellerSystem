using Microsoft.EntityFrameworkCore;
using ResellerSystem.Modules.Inventory.Domain;

namespace ResellerSystem.Modules.Inventory.Data;

/// <summary>
/// The Inventory module's own EF Core context — separate from Server.Data's
/// TenantDbContext by design (Product Development Plan v1.0, Part 2.1:
/// each module owns its schema/context independently). Schema is created
/// by SqlScriptMigrationRunner from this project's embedded
/// Migrations/Scripts/Tenant/inventory/*.sql; this context only maps it.
/// </summary>
public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseItemLine> PurchaseItemLines => Set<PurchaseItemLine>();
    public DbSet<PurchaseExpenseLine> PurchaseExpenseLines => Set<PurchaseExpenseLine>();
    public DbSet<ReferenceListValue> ReferenceListValues => Set<ReferenceListValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
            entity.Property(e => e.SourceName).HasColumnName("source_name").IsRequired();
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.SalesTaxAmount).HasColumnName("sales_tax_amount");
            entity.Property(e => e.SalesTaxRate).HasColumnName("sales_tax_rate");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method");
            entity.Property(e => e.UsedResellerPermit).HasColumnName("used_reseller_permit");
            entity.Property(e => e.PurchaseType).HasColumnName("purchase_type");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.MerchandiseSubtotal).HasColumnName("merchandise_subtotal");
            entity.Property(e => e.TaxableAmount).HasColumnName("taxable_amount");
            entity.Property(e => e.SalesTaxAmountCalculated).HasColumnName("sales_tax_amount_calculated");
            entity.Property(e => e.SalesTaxIsManualOverride).HasColumnName("sales_tax_is_manual_override");
            entity.Property(e => e.SalesTaxAllocationMethod).HasColumnName("sales_tax_allocation_method");
            entity.Property(e => e.ExpenseAllocationMethod).HasColumnName("expense_allocation_method");
            entity.Property(e => e.ManualAdjustment).HasColumnName("manual_adjustment");
            entity.Property(e => e.PermitNumber).HasColumnName("permit_number");
            entity.Property(e => e.PermitDate).HasColumnName("permit_date");
            entity.Property(e => e.TaxExemptAmount).HasColumnName("tax_exempt_amount");
            entity.Property(e => e.SourceType).HasColumnName("source_type");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasMany(e => e.Items).WithOne(e => e.Purchase).HasForeignKey(e => e.PurchaseId);
            entity.HasMany(e => e.ItemLines).WithOne().HasForeignKey(e => e.PurchaseId);
            entity.HasMany(e => e.ExpenseLines).WithOne().HasForeignKey(e => e.PurchaseId);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ItemNumber).HasColumnName("item_number").ValueGeneratedOnAdd();
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.PurchaseItemLineId).HasColumnName("purchase_item_line_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CategoryName).HasColumnName("category_name");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CostBasisCalculated).HasColumnName("cost_basis_calculated");
            entity.Property(e => e.CostBasisOverride).HasColumnName("cost_basis_override");
            entity.Ignore(e => e.EffectiveCostBasis);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Brand).HasColumnName("brand");
            entity.Property(e => e.Model).HasColumnName("model");
            entity.Property(e => e.SerialNumber).HasColumnName("serial_number");
            entity.Property(e => e.SkuCustomLabel).HasColumnName("sku_custom_label");
            entity.Property(e => e.Condition).HasColumnName("condition");
            entity.Property(e => e.StorageLocation).HasColumnName("storage_location");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasIndex(e => e.ItemNumber).IsUnique();

            // EF must know about this FK (it exists at the DB level via
            // 0004_purchase_lines.sql's REFERENCES clause) so it inserts the
            // parent PurchaseItemLine row before any Item row that points to
            // it within the same SaveChangesAsync — without this, EF has no
            // dependency edge between the two tables' insert batches and can
            // (and did) execute them in the wrong order, violating the FK.
            entity.HasOne<PurchaseItemLine>()
                .WithMany()
                .HasForeignKey(e => e.PurchaseItemLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<PurchaseItemLine>(entity =>
        {
            entity.ToTable("purchase_item_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.LineNumber).HasColumnName("line_number");
            entity.Property(e => e.ItemName).HasColumnName("item_name").IsRequired();
            entity.Property(e => e.CategoryName).HasColumnName("category_name");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPurchaseCost).HasColumnName("unit_purchase_cost");
            entity.Property(e => e.LinePurchaseCost).HasColumnName("line_purchase_cost");
            entity.Property(e => e.AllocatedSalesTax).HasColumnName("allocated_sales_tax");
            entity.Property(e => e.ManualAllocatedSalesTax).HasColumnName("manual_allocated_sales_tax");
            entity.Property(e => e.AllocatedExpenses).HasColumnName("allocated_expenses");
            entity.Property(e => e.ManualAllocatedExpenses).HasColumnName("manual_allocated_expenses");
            entity.Property(e => e.FinalLineCostBasis).HasColumnName("final_line_cost_basis");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<PurchaseExpenseLine>(entity =>
        {
            entity.ToTable("purchase_expense_lines");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.ExpenseType).HasColumnName("expense_type").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            // No soft-delete — Purchase updates fully replace this set.
        });

        modelBuilder.Entity<ReferenceListValue>(entity =>
        {
            entity.ToTable("reference_list_values");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ListKey).HasColumnName("list_key").IsRequired();
            entity.Property(e => e.Value).HasColumnName("value").IsRequired();
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.IsSystemDefault).HasColumnName("is_system_default");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }
}
