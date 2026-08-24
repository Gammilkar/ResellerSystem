using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Server.Infrastructure.Security;

/// <summary>
/// Stage 1 implementation: a single fixed local system user with access to
/// every database. Everything that consumes ICurrentUserContext is written
/// against the interface, so swapping this for a real authenticated-user
/// implementation later requires no changes outside DI registration.
/// </summary>
public sealed class SystemCurrentUserContext : ICurrentUserContext
{
    public string UserId => "local-system-user";
    public string DisplayName => "Local User";
    public bool CanAccessAllDatabases => true;
    public IReadOnlyCollection<Guid> AllowedDatabaseIds => Array.Empty<Guid>();
}
