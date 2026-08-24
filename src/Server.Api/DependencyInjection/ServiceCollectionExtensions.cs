using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Server.Api.Middleware;
using ResellerSystem.Server.Api.Security;
using ResellerSystem.Server.Application.Common;
using ResellerSystem.Server.Application.Databases.Validators;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.Data;
using ResellerSystem.Server.Domain.Abstractions;
using ResellerSystem.Server.FileStorage;
using ResellerSystem.Server.Infrastructure.Clock;
using ResellerSystem.Server.Infrastructure.Configuration;
using ResellerSystem.Server.Infrastructure.Security;
using ResellerSystem.Server.Infrastructure.Time;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Server.Api.DependencyInjection;

/// <summary>
/// Composition root for everything the API needs. Server.Host calls
/// AddServerApiServices(...) then UseServerApiPipeline(...) — it never wires
/// individual layers itself, so the dependency graph lives in one place.
///
/// "Thin API Gateway": Server.Api itself only owns Core concerns (Health,
/// Version, Databases — see Controllers/). Everything module-specific is
/// registered/mounted through <see cref="IResellerModule"/> instances that
/// Server.Host passes in — Server.Api never references a concrete module
/// project (see Product Development Plan v1.0, Part 2.1).
/// </summary>
public static class ServiceCollectionExtensions
{
    public const string CorsPolicyName = "ServerApiCors";

    public static IServiceCollection AddServerApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<IResellerModule> modules)
    {
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<VersionOptions>(configuration.GetSection(VersionOptions.SectionName));

        // Cross-cutting infrastructure implementations
        services.AddSingleton<ITimeZoneValidator, TimeZoneValidator>();
        services.AddSingleton<ICurrencyValidator, CurrencyValidator>();
        services.AddSingleton<IClock, ResellerSystem.Server.Infrastructure.Clock.SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<Application.Databases.ICurrentTenantAccessor, Application.Databases.CurrentTenantAccessor>();
        services.AddSingleton<IVersionProvider, VersionProvider>();

        services.AddAuthentication(SessionAuthenticationOptions.SchemeName)
            .AddScheme<SessionAuthenticationOptions, SessionAuthenticationHandler>(
                SessionAuthenticationOptions.SchemeName, _ => { });
        services.AddAuthorization();

        services.AddSingleton<IFileStorageService>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;
            var storage = new LocalFileStorageService(options.StorageRoot, options.BackupRoot, options.UpdateRoot, options.TempRoot);
            storage.EnsureRootsExist();
            return storage;
        });

        // Persistence + business services (Server.Data composition)
        services.AddServerData(configuration);

        // Module catalog — registered here (not resolved from a
        // half-built container) so TenantDatabaseProvisioner and the
        // endpoint-mounting step below can both depend on it via normal DI.
        services.AddSingleton<IServerModuleCatalog>(new StaticServerModuleCatalog(modules));
        foreach (var module in modules)
        {
            module.RegisterServices(services);
        }

        // Validation is performed explicitly inside application services
        // (see DatabaseProvisioningService), which throw ValidationFailedException.
        // Automatic MVC model-validation is intentionally NOT enabled here —
        // it would short-circuit with ASP.NET's own ValidationProblemDetails
        // format before reaching the service layer, bypassing the uniform
        // ApiErrorResponse envelope required by the architecture.
        services.AddValidatorsFromAssemblyContaining<CreateDatabaseRequestValidator>();

        // API plumbing
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        var corsOrigins = configuration.GetSection(ServerOptions.SectionName + ":AllowedCorsOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                // Native desktop/mobile clients don't send an Origin header and are
                // unaffected by CORS. This policy only matters for a future web
                // client and is intentionally NOT AllowAnyOrigin in production.
                if (corsOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => false);
                }
            });
        });

        return services;
    }

    public static WebApplication UseServerApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors(CorsPolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.MapControllers();

        // Mount every module's own endpoints (conventionally under
        // /api/v1/{ModuleKey}/...). The catalog now includes Inventory,
        // Sales, Expenses, Documents, Reports, and Import (see
        // Server.Host/Program.cs) — adding another module requires zero
        // changes here, only a new entry in that list.
        var moduleCatalog = app.Services.GetRequiredService<IServerModuleCatalog>();
        foreach (var module in moduleCatalog.Modules)
        {
            module.MapEndpoints(app);
        }

        return app;
    }
}
