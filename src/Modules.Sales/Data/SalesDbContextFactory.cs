using Microsoft.EntityFrameworkCore;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Sales.Data;

public interface ISalesDbContextFactory
{
    SalesDbContext CreateForCurrentTenant();
}

public sealed class SalesDbContextFactory : ISalesDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public SalesDbContextFactory(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public SalesDbContext CreateForCurrentTenant()
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var options = new DbContextOptionsBuilder<SalesDbContext>().UseNpgsql(connectionString).Options;
        return new SalesDbContext(options);
    }
}
