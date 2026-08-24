namespace ResellerSystem.Server.Application.Databases;

/// <summary>
/// Server-side authority over "which tenant database does this request use".
/// Clients only ever send a Database Id (a Guid); this resolver is the single
/// place that decides whether that Id is valid, active, ready, and allowed
/// for the current caller — it is never trusted blindly (see architecture
/// requirement: no naive "X-Database-Id, trust it" model).
/// </summary>
public interface IDatabaseContextResolver
{
    /// <summary>
    /// Resolves and validates a requested database Id.
    /// Throws NotFoundException if it doesn't exist, DatabaseNotReadyException
    /// if it exists but isn't Ready/Active, and (once real users exist)
    /// will throw an authorization exception if the current user isn't allowed.
    /// </summary>
    Task<ResolvedTenantContext> ResolveAsync(Guid databaseId, CancellationToken ct = default);
}

/// <summary>
/// Everything Server.Data needs to open a tenant connection, and nothing more —
/// in particular this never carries a raw connection string or credentials
/// out past Server.Data.
/// </summary>
public sealed record ResolvedTenantContext(Guid DatabaseId, string PhysicalDatabaseName, string DisplayName);
