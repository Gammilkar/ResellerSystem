namespace ResellerSystem.Server.Data.Tenant;

/// <summary>
/// Creates a TenantDbContext bound to a specific physical database.
/// Always go through IDatabaseContextResolver first to validate the
/// requested Database Id before calling this — this factory trusts its
/// input completely.
/// </summary>
public interface ITenantDbContextFactory
{
    TenantDbContext CreateDbContext(string physicalDatabaseName);
}
