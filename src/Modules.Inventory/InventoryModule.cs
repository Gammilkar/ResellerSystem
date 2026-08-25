using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Inventory;

/// <summary>
/// First real implementation of IResellerModule — proves the module
/// contract end-to-end (own migrations, own DbContext, own API, own
/// version) established during Platform Refactor. Registered by
/// Server.Host: modules.Add(new InventoryModule()).
/// </summary>
public sealed class InventoryModule : IResellerModule
{
    public string ModuleKey => "inventory";
    public string DisplayName => "Inventory";
    public string Version => "0.2.0";
    public string MinimumCoreVersion => "0.1.0";

    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Inventory";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IInventoryDbContextFactory, InventoryDbContextFactory>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IInventoryTableReader, InventoryTableReader>();

        // Controllers in this assembly (Api/InventoryControllers.cs) are
        // discovered by ASP.NET Core's default MVC assembly scan only if
        // this assembly is added as an application part — Server.Api's
        // AddControllers() does NOT automatically see Modules.Inventory's
        // types, so we register it explicitly here rather than requiring
        // Server.Api to know about the module's assembly.
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Controllers are mapped by the shared app.MapControllers() call in
        // Server.Api's UseServerApiPipeline (since they're now a registered
        // application part) — nothing additional to map here for this
        // module. Left as a no-op intentionally rather than removed, so the
        // interface's intent (a module CAN map raw minimal-API endpoints
        // too, not just controllers) stays visible for future modules.
    }
}
