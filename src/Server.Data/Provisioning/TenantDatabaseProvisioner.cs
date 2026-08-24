using System.Reflection;
using Npgsql;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Migrations;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Server.Data.Provisioning;

/// <summary>
/// Talks to PostgreSQL directly (not through EF Core) for the operations
/// that EF Core cannot perform: CREATE DATABASE must run outside a
/// transaction against the "postgres" maintenance database, never against
/// the target database itself.
///
/// Tenant migrations are now applied per module: the built-in "core"
/// module (this project's own Migrations/Scripts/Tenant/core/*.sql) always
/// runs first, followed by every module in IServerModuleCatalog — even
/// though the catalog is empty until the first business module ships
/// (Product Development Plan Part 3, step 5+), the mechanism is exercised
/// end-to-end today via the core module alone.
/// </summary>
public sealed class TenantDatabaseProvisioner : ITenantDatabaseProvisioner
{
    private const string CoreRootNamespace = "ResellerSystem.Server.Data";

    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly IMigrationRunner _migrationRunner;
    private readonly IServerModuleCatalog _moduleCatalog;

    public TenantDatabaseProvisioner(
        ConnectionStringFactory connectionStringFactory,
        IMigrationRunner migrationRunner,
        IServerModuleCatalog moduleCatalog)
    {
        _connectionStringFactory = connectionStringFactory;
        _migrationRunner = migrationRunner;
        _moduleCatalog = moduleCatalog;
    }

    public async Task<bool> DatabaseExistsAsync(string physicalDatabaseName, CancellationToken ct = default)
    {
        var connectionString = _connectionStringFactory.BuildMaintenanceConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name;", connection);
        cmd.Parameters.AddWithValue("name", physicalDatabaseName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task CreateDatabaseAsync(string physicalDatabaseName, CancellationToken ct = default)
    {
        // Identifiers cannot be parameterized in DDL; physicalDatabaseName is
        // always server-generated ("reseller_db_000015"), never user input,
        // so a conservative allow-list check is sufficient defense in depth.
        if (!IsSafePhysicalName(physicalDatabaseName))
        {
            throw new InvalidOperationException($"Refusing to create database with unsafe name '{physicalDatabaseName}'.");
        }

        var connectionString = _connectionStringFactory.BuildMaintenanceConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{physicalDatabaseName}\";", connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Applies the "core" module's tenant migrations, then every module in
    /// the catalog, in registration order. Returns the resulting "core"
    /// module version — DatabaseProfile.SchemaVersion is documented as
    /// meaning specifically that (see ITenantDatabaseProvisioner); the full
    /// per-module picture lives in tenant_module_versions and is readable
    /// via IMigrationRunner.GetTenantModuleVersionsAsync.
    /// </summary>
    public async Task<int> ApplyTenantMigrationsAsync(string physicalDatabaseName, CancellationToken ct = default)
    {
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(physicalDatabaseName);

        var coreTarget = new ModuleMigrationTarget(
            SqlScriptMigrationRunner.CoreModuleKey,
            Assembly.GetExecutingAssembly(),
            CoreRootNamespace);
        var coreVersion = await _migrationRunner.ApplyTenantModuleMigrationsAsync(coreTarget, connectionString, ct);

        foreach (var module in _moduleCatalog.Modules)
        {
            var target = new ModuleMigrationTarget(module.ModuleKey, module.MigrationsAssembly, module.MigrationsRootNamespace);
            await _migrationRunner.ApplyTenantModuleMigrationsAsync(target, connectionString, ct);
        }

        return coreVersion;
    }

    private static bool IsSafePhysicalName(string name) =>
        name.Length > 0 && name.Length <= 63 &&
        name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_');
}
