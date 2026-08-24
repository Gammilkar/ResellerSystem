using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Expenses.Application;
using ResellerSystem.Modules.Expenses.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Expenses;

public sealed class ExpensesModule : IResellerModule
{
    public string ModuleKey => "expenses";
    public string DisplayName => "Expenses";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";
    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Expenses";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IExpensesDbContextFactory, ExpensesDbContextFactory>();
        services.AddScoped<IExpensesService, ExpensesService>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
