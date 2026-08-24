using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Modules.Documents.Application;
using ResellerSystem.Modules.Documents.Data;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Modules.Documents;

public sealed class DocumentsModule : IResellerModule
{
    public string ModuleKey => "documents";
    public string DisplayName => "Documents";
    public string Version => "0.1.0";
    public string MinimumCoreVersion => "0.1.0";
    public Assembly MigrationsAssembly => Assembly.GetExecutingAssembly();
    public string MigrationsRootNamespace => "ResellerSystem.Modules.Documents";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IDocumentsDbContextFactory, DocumentsDbContextFactory>();
        services.AddScoped<IDocumentsService, DocumentsService>();
        services.AddControllers().AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
