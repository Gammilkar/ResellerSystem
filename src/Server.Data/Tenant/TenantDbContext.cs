using Microsoft.EntityFrameworkCore;

namespace ResellerSystem.Server.Data.Tenant;

/// <summary>
/// EF Core context for a single tenant ("business") database. Intentionally
/// near-empty at Stage 1 — Purchase/Item/Listing/Sale/etc. are added here in
/// later stages as DbSets + Fluent configuration, following the same
/// "SQL script defines schema, EF Core maps it" pattern as MasterDbContext.
/// </summary>
public sealed class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    // Future: DbSet<Purchase>, DbSet<Item>, DbSet<Listing>, DbSet<Sale>, ...
}
