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

    private readonly ConnectionStringFactory _connectionStringFactory;

    public SessionService(ConnectionStringFactory connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public async Task<SessionInfo> CreateSessionAsync(Guid userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);

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

    public async Task<Guid?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id FROM sessions WHERE token = @t AND expires_at > now();", connection);
        cmd.Parameters.AddWithValue("t", token);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid userId ? userId : null;
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
