namespace ResellerSystem.Server.Domain.Abstractions;

/// <summary>
/// Abstraction over "who is making this request". On this stage there is no
/// login, so the only implementation returns a fixed local system user.
/// Application/API code must depend on this interface — never assume a
/// single hardcoded user — so real authentication can be introduced later
/// without touching business logic or controllers.
/// </summary>
public interface ICurrentUserContext
{
    string UserId { get; }
    string DisplayName { get; }

    /// <summary>
    /// Databases this user may access. Today this always returns "all",
    /// but the shape already models per-user access for future roles.
    /// </summary>
    bool CanAccessAllDatabases { get; }
    IReadOnlyCollection<Guid> AllowedDatabaseIds { get; }
}
