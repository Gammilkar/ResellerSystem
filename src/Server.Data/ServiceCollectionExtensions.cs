using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Backup;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Modules;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Application.Update;
using ResellerSystem.Server.Data.Audit;
using ResellerSystem.Server.Data.Backup;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.Data.Migrations;
using ResellerSystem.Server.Data.Modules;
using ResellerSystem.Server.Data.Provisioning;
using ResellerSystem.Server.Data.Repositories;
using ResellerSystem.Server.Data.Security;
using ResellerSystem.Server.Data.Tenant;
using ResellerSystem.Server.Data.Update;

namespace ResellerSystem.Server.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PostgresOptions>(configuration.GetSection(PostgresOptions.SectionName));
        services.AddSingleton<ConnectionStringFactory>();

        services.AddDbContext<MasterDbContext>((sp, options) =>
        {
            var factory = sp.GetRequiredService<ConnectionStringFactory>();
            options.UseNpgsql(factory.BuildMasterConnectionString());
        });

        services.AddSingleton<IMigrationRunner, SqlScriptMigrationRunner>();
        services.AddScoped<MasterDatabaseInitializer>();
        services.AddScoped<IMasterDatabaseHealthChecker, MasterDatabaseHealthChecker>();
        services.AddScoped<IModuleRegistry, ModuleRegistry>();

        services.AddScoped<IDatabaseProfileRepository, DatabaseProfileRepository>();
        services.AddScoped<ITenantDatabaseProvisioner, TenantDatabaseProvisioner>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        services.AddScoped<IDatabaseProvisioningService, DatabaseProvisioningService>();
        services.AddScoped<IDatabaseContextResolver, DatabaseContextResolver>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        services.AddScoped<IBackupService, PgBackupService>();

        services.AddScoped<IAuditLogger, AuditLogger>();

        services.Configure<UpdateOptions>(configuration.GetSection(UpdateOptions.SectionName));
        services.AddHttpClient(nameof(UpdateService));
        services.AddScoped<IUpdateService, UpdateService>();

        return services;
    }
}
