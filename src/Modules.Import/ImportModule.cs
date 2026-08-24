using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Import.Application;
using ResellerSystem.Modules.Import.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Import;

/// <summary>
/// The one module that intentionally depends on another module directly
/// (Modules.Inventory's IInventoryService) — see the .csproj comment and
/// KNOWN_LIMITATIONS.md "Import module scope" for why. Requires
/// InventoryModule to be registered in Server.Host's module list BEFORE
/// this one (already true — see Program.cs ordering).
/// </summary>
public sealed class ImportModule : IResellerModule
{
    public string ModuleKey => "import";
    public string DisplayName => "Import";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";
    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Import";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IImportDbContextFactory, ImportDbContextFactory>();
        services.AddScoped<IImportService, ImportService>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
