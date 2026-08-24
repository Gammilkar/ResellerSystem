namespace ResellerSystem.Server.Application.Databases;

/// <summary>
/// Scoped holder for "which tenant database this request is using" — set
/// once per request by Server.Api's TenantResolutionMiddleware after it
/// validates the client-supplied X-Database-Id header through
/// IDatabaseContextResolver (never trusted blindly — see that interface's
/// docs). Module controllers/services read from here instead of each
/// re-implementing header parsing + resolution.
/// </summary>
public interface ICurrentTenantAccessor
{
    ResolvedTenantContext? Current { get; set; }

    /// <summary>Throws if no valid tenant was resolved for this request —
    /// use from module endpoints that always require a database, so a
    /// missing/invalid X-Database-Id fails fast with a clear error instead
    /// of a null-reference deeper in the call stack.</summary>
    ResolvedTenantContext Require();
}

public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public ResolvedTenantContext? Current { get; set; }

    public ResolvedTenantContext Require() => Current
        ?? throw new Exceptions.DatabaseNotReadyException(
            "This request requires a valid X-Database-Id header identifying an active database.");
}
