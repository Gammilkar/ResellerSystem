namespace ResellerSystem.Server.Application.Security;

public sealed record SessionInfo(string Token, Guid UserId, DateTimeOffset ExpiresAt);

/// <summary>
/// Server-side session tokens (opaque random strings, stored in the master
/// DB `sessions` table — not JWTs). Deliberately simple: this is a single
/// local server, not a distributed system that needs stateless tokens.
/// </summary>
public interface ISessionService
{
    Task<SessionInfo> CreateSessionAsync(Guid userId, CancellationToken ct = default);
    Task<Guid?> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}
