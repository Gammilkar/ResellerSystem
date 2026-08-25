using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Import.Application;
using ResellerSystem.Modules.Import.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Import;

/// <summary>
/// The one module that intentionally depends on other modules directly
/// (IInventoryService, ISalesService, IExpensesService) — see the .csproj
/// comment and KNOWN_LIMITATIONS.md "Import module scope" for why.
/// Requires Inventory/Sales/Expenses modules to already be registered in
/// Server.Host's module list before this one.
/// </summary>
public sealed class ImportModule : IResellerModule
{
    public string ModuleKey => "import";
    public string DisplayName => "Import";
    public string Version => "0.2.0";
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
