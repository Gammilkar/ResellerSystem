using Microsoft.EntityFrameworkCore;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Inventory.Data;

public interface IInventoryDbContextFactory
{
    InventoryDbContext CreateForCurrentTenant();
}

/// <summary>
/// Builds an InventoryDbContext pointed at whichever tenant database
/// TenantResolutionMiddleware resolved for the current request (via
/// X-Database-Id) — see ICurrentTenantAccessor. Every module that needs
/// its own DbContext follows this same small pattern.
/// </summary>
public sealed class InventoryDbContextFactory : IInventoryDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public InventoryDbContextFactory(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public InventoryDbContext CreateForCurrentTenant()
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(connectionString).Options;
        return new InventoryDbContext(options);
    }
}
