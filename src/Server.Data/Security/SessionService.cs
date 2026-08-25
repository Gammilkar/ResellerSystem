using System.Security.Cryptography;
using Npgsql;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Server.Data.Security;

/// <summary>Raw-SQL against `sessions` (master DB) — a two-column
/// token->user table doesn't need EF mapping ceremony, matching the
/// approach already taken for `installed_modules`/ModuleRegistry.</summary>
public sealed class SessionService : ISessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    /// <summary>"Trusted device" lifetime — see LoginRequest.RememberMe.
    /// The client persists a session created with this lifetime to local
    /// disk (DPAPI-encrypted, current Windows user only) so it can skip
    /// the password prompt on next launch, up until this expiry.</summary>
    private static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(30);

    private readonly ConnectionStringFactory _connectionStringFactory;

    public SessionService(ConnectionStringFactory connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public async Task<SessionInfo> CreateSessionAsync(Guid userId, bool rememberMe, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expiresAt = DateTimeOffset.UtcNow.Add(rememberMe ? RememberMeLifetime : SessionLifetime);

        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO sessions (token, user_id, created_at, expires_at) VALUES (@t, @u, now(), @e);", connection);
        cmd.Parameters.AddWithValue("t", token);
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("e", expiresAt);
        await cmd.ExecuteNonQueryAsync(ct);

        return new SessionInfo(token, userId, expiresAt);
    }

    public async Task<ValidatedSession?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT u.id, u.username
            FROM sessions s
            JOIN users u ON u.id = s.user_id
            WHERE s.token = @t AND s.expires_at > now();
            """, connection);
        cmd.Parameters.AddWithValue("t", token);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new ValidatedSession(reader.GetGuid(0), reader.GetString(1));
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("DELETE FROM sessions WHERE token = @t;", connection);
        cmd.Parameters.AddWithValue("t", token);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
