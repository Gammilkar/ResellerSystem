using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Reports.Application;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Reports;

/// <summary>Purely a read-only reporting module — no tenant migrations of
/// its own (queries Inventory/Sales tables directly, see ReportsService).</summary>
public sealed class ReportsModule : IResellerModule
{
    public string ModuleKey => "reports";
    public string DisplayName => "Reports";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";
    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Reports"; // no scripts present — zero migrations is valid

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IReportsService, ReportsService>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
