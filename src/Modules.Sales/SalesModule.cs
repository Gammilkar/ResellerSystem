using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Sales.Application;
using ResellerSystem.Modules.Sales.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Sales;

public sealed class SalesModule : IResellerModule
{
    public string ModuleKey => "sales";
    public string DisplayName => "Sales";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";

    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Sales";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<ISalesDbContextFactory, SalesDbContextFactory>();
        services.AddScoped<IItemCostBasisReader, ItemCostBasisReader>();
        services.AddScoped<ISalesService, SalesService>();

        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // See InventoryModule's identical comment — controllers are mapped
        // by the shared app.MapControllers() call once registered as an
        // application part above.
    }
}
