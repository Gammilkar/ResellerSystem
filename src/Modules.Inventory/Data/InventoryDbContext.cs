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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
            entity.Property(e => e.SourceName).HasColumnName("source_name").IsRequired();
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.SalesTaxAmount).HasColumnName("sales_tax_amount");
            entity.Property(e => e.SalesTaxRate).HasColumnName("sales_tax_rate");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method");
            entity.Property(e => e.UsedResellerPermit).HasColumnName("used_reseller_permit");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasMany(e => e.Items).WithOne(e => e.Purchase).HasForeignKey(e => e.PurchaseId);
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ItemNumber).HasColumnName("item_number").ValueGeneratedOnAdd();
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.CategoryName).HasColumnName("category_name");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CostBasisCalculated).HasColumnName("cost_basis_calculated");
            entity.Property(e => e.CostBasisOverride).HasColumnName("cost_basis_override");
            entity.Ignore(e => e.EffectiveCostBasis);
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasIndex(e => e.ItemNumber).IsUnique();
        });
    }
}
