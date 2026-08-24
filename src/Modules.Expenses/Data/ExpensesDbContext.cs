using Microsoft.EntityFrameworkCore;
using ResellerSystem.Modules.Expenses.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Expenses.Data;

public sealed class ExpensesDbContext : DbContext
{
    public ExpensesDbContext(DbContextOptions<ExpensesDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("expenses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ExpenseType).HasColumnName("expense_type").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ExpenseDate).HasColumnName("expense_date");
            entity.Property(e => e.PurchaseId).HasColumnName("purchase_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.SaleId).HasColumnName("sale_id");
            entity.Property(e => e.ReturnId).HasColumnName("return_id");
            entity.Property(e => e.PaymentMethod).HasColumnName("payment_method");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }
}

public interface IExpensesDbContextFactory
{
    ExpensesDbContext CreateForCurrentTenant();
}

public sealed class ExpensesDbContextFactory : IExpensesDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public ExpensesDbContextFactory(
        ConnectionStringFactory connectionStringFactory,
        ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public ExpensesDbContext CreateForCurrentTenant()
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var options = new DbContextOptionsBuilder<ExpensesDbContext>().UseNpgsql(connectionString).Options;
        return new ExpensesDbContext(options);
    }
}
