using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Dashboard.Application;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Dashboard;

/// <summary>Purely a read-only module — no tenant migrations of its own
/// (queries Inventory/Sales tables directly, see DashboardService, same
/// pattern as Modules.Reports).</summary>
public sealed class DashboardModule : IResellerModule
{
    public string ModuleKey => "dashboard";
    public string DisplayName => "Dashboard";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";
    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Dashboard"; // no scripts present — zero migrations is valid

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
