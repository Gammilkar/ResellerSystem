using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ResellerSystem.Server.Modules.Abstractions;

/// <summary>
/// Contract every business module (Inventory, Sales, Expenses, ...) must
/// implement. A module is ONE .NET project (Domain/Application/Data/Api as
/// folders inside it, not separate assemblies — see Product Development
/// Plan v1.0, Part 2.1: per-module multi-project split is unnecessary
/// overengineering at this scale).
///
/// Modules are discovered statically: Server.Host references each module
/// project directly and builds the list passed to
/// <see cref="IServerModuleCatalog"/> — there is no runtime DLL-scanning
/// "plugin loader". That keeps the system simple and debuggable while still
/// giving every module its own migrations, API surface, and version.
///
/// Core (Database Engine, Update Engine, Backup Engine, Migration Engine,
/// Authentication foundation, ...) is NOT a module — it is always present
/// and is not represented by this interface.
/// </summary>
public interface IResellerModule
{
    /// <summary>Stable, lowercase, URL-safe key — e.g. "inventory", "sales".
    /// Used as: API route prefix (/api/v1/{key}/...), tenant migration
    /// folder name, and the primary key in installed_modules /
    /// tenant_module_versions.</summary>
    string ModuleKey { get; }

    /// <summary>Human-readable name for Settings/Server Manager UI.</summary>
    string DisplayName { get; }

    /// <summary>Module version — MAJOR.MINOR.PATCH, versioned independently
    /// of the Core/product version even though it currently ships on the
    /// same release train (see Release Strategy, Part 2.6).</summary>
    string Version { get; }

    /// <summary>Minimum Core/product version this module requires. The
    /// module host refuses to load a module whose requirement exceeds the
    /// running Core version, rather than starting in a broken state.</summary>
    string MinimumCoreVersion { get; }

    /// <summary>Registers the module's own services (repositories,
    /// application services, validators, ...) into the shared container.
    /// Must NOT register anything Core already owns (DbContexts, logging,
    /// etc.) — modules consume Core services, they don't redefine them.</summary>
    void RegisterServices(IServiceCollection services);

    /// <summary>Maps the module's HTTP endpoints, conventionally under
    /// "/api/v1/{ModuleKey}/...". Called once per request pipeline setup.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>
    /// Assembly containing this module's embedded tenant migration scripts,
    /// under the resource path
    /// "{MigrationsRootNamespace}.Migrations.Scripts.Tenant.{ModuleKey}.NNNN_*.sql".
    /// Usually just <c>typeof(SomeTypeInThisModule).Assembly</c>.
    /// </summary>
    Assembly MigrationsAssembly { get; }

    /// <summary>
    /// The module project's &lt;RootNamespace&gt; exactly as set in its
    /// .csproj — embedded resource logical names are generated from this,
    /// NOT from the assembly name, and the two can differ (this bit the
    /// Core migration runner once already; every module must state it
    /// explicitly rather than have the runner guess).
    /// </summary>
    string MigrationsRootNamespace { get; }
}
