using Microsoft.EntityFrameworkCore;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Server.Data.Tenant;

public sealed class TenantDbContextFactory : ITenantDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;

    public TenantDbContextFactory(ConnectionStringFactory connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public TenantDbContext CreateDbContext(string physicalDatabaseName)
    {
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(physicalDatabaseName);
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TenantDbContext(options);
    }
}
