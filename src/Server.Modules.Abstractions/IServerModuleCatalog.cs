namespace ResellerSystem.Server.Modules.Abstractions;

/// <summary>
/// The statically-known list of business modules built into this
/// installation. Implemented once, in Server.Host (the only project
/// allowed to reference concrete module assemblies), and consumed via DI
/// by everything else (Server.Api for endpoint/service registration,
/// Server.Data for migrations) — so Server.Api/Server.Data never need a
/// project reference to any concrete module.
/// </summary>
public interface IServerModuleCatalog
{
    IReadOnlyList<IResellerModule> Modules { get; }
}

/// <summary>Stage: Platform Refactor — no business modules exist yet.
/// Server.Host wires this up (see composition root) with an empty list
/// today; module projects are added to the list one at a time as they're
/// built (Inventory first, per Product Development Plan Part 3).</summary>
public sealed class StaticServerModuleCatalog : IServerModuleCatalog
{
    public StaticServerModuleCatalog(IEnumerable<IResellerModule> modules)
    {
        Modules = modules.ToList();
    }

    public IReadOnlyList<IResellerModule> Modules { get; }
}
