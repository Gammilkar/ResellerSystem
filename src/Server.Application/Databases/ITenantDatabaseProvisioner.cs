namespace ResellerSystem.Server.Application.Databases;

/// <summary>
/// Low-level PostgreSQL operations needed to provision a tenant database.
/// Implemented in Server.Data using Npgsql against the "postgres"
/// maintenance database (CREATE DATABASE cannot run inside a transaction
/// or against the target DB itself).
/// </summary>
public interface ITenantDatabaseProvisioner
{
    Task<bool> DatabaseExistsAsync(string physicalDatabaseName, CancellationToken ct = default);
    Task CreateDatabaseAsync(string physicalDatabaseName, CancellationToken ct = default);

    /// <summary>
    /// Applies all pending tenant migration scripts to the given physical
    /// database — the built-in "core" module first, then every module in
    /// IServerModuleCatalog — and returns the resulting "core" module
    /// schema version specifically (not an aggregate across modules; the
    /// full per-module picture is tracked in tenant_module_versions).
    /// Throws if any script fails — caller is responsible for marking the
    /// tenant as MigrationFailed rather than Ready.
    /// </summary>
    Task<int> ApplyTenantMigrationsAsync(string physicalDatabaseName, CancellationToken ct = default);
}
