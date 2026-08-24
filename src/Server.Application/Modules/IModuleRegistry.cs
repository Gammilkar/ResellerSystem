namespace ResellerSystem.Server.Application.Modules;

public sealed record InstalledModuleInfo(string ModuleKey, string Version, DateTimeOffset InstalledAt, DateTimeOffset UpdatedAt);

/// <summary>
/// Read/write access to the master-database `installed_modules` registry —
/// the server-level record of which modules (including the built-in
/// "core" pseudo-module) are installed and at which version. Distinct from
/// `tenant_module_versions` (per-tenant-database schema version, see
/// IMigrationRunner): this registry answers "what is this SERVER
/// installation running", the tenant table answers "has THIS BUSINESS
/// DATABASE been migrated to match".
/// </summary>
public interface IModuleRegistry
{
    Task<IReadOnlyList<InstalledModuleInfo>> GetInstalledModulesAsync(CancellationToken ct = default);

    /// <summary>Upserts a module's recorded version — called at Server.Host
    /// startup for "core" and every catalog module, and again after a
    /// successful Update Engine install.</summary>
    Task RegisterOrUpdateAsync(string moduleKey, string version, CancellationToken ct = default);
}
