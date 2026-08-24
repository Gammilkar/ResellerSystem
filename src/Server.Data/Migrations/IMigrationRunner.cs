using System.Reflection;

namespace ResellerSystem.Server.Data.Migrations;

/// <summary>Identifies which assembly/namespace/folder a module's tenant
/// migration scripts live in. See IResellerModule.MigrationsAssembly /
/// MigrationsRootNamespace — this is the same information, just also
/// usable for the built-in "core" pseudo-module (which lives inside
/// Server.Data itself rather than a separate module assembly).</summary>
public sealed record ModuleMigrationTarget(string ModuleKey, Assembly Assembly, string RootNamespace);

/// <summary>
/// Applies versioned, append-only SQL scripts and records what has been
/// applied, so re-running is always safe (idempotent) and partial
/// application is detectable.
///
/// Two tracking mechanisms, matching the two things that own schema:
///   - Master DB: flat versioning via `schema_migrations` — master only
///     ever holds Core tables (database_profiles, installed_modules), it
///     is not multi-module.
///   - Tenant DB: per-module versioning via `tenant_module_versions`
///     (module_key, script_version) — every module, including the
///     built-in "core" module, tracks its own applied scripts
///     independently, so modules can evolve their schema on independent
///     timelines even though they currently ship on one release train.
///
/// Chosen over EF Core Migrations because it needs to run programmatically
/// against dozens of tenant databases (and, per module, against assemblies
/// that aren't known until Server.Host builds its module list) without
/// `dotnet ef` tooling or a design-time DbContext per module.
/// </summary>
public interface IMigrationRunner
{
    /// <summary>Applies pending Master scripts (from Server.Data's own
    /// assembly). Returns the resulting flat schema version.</summary>
    Task<int> ApplyMasterMigrationsAsync(string connectionString, CancellationToken ct = default);

    /// <summary>Applies pending scripts for one module's tenant schema.
    /// Returns the resulting version for that module (highest applied
    /// script number; 0 if the module has no scripts at all).</summary>
    Task<int> ApplyTenantModuleMigrationsAsync(ModuleMigrationTarget module, string connectionString, CancellationToken ct = default);

    /// <summary>Current version per module_key, as recorded in
    /// tenant_module_versions. Empty dictionary if the database hasn't
    /// been migrated at all yet.</summary>
    Task<IReadOnlyDictionary<string, int>> GetTenantModuleVersionsAsync(string connectionString, CancellationToken ct = default);
}
