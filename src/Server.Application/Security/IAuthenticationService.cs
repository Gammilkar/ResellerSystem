namespace ResellerSystem.Server.Application.Security;

public sealed record LoginResult(bool Success, string? Token, DateTimeOffset? ExpiresAt, string? FailureReason);

public sealed record ChangePasswordResult(bool Success, string? FailureReason);

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(string username, string password, bool rememberMe, CancellationToken ct = default);

    /// <summary>True if no user exists yet — the server is in first-run
    /// setup state and Server Manager should prompt to create the initial
    /// admin account rather than showing a login screen.</summary>
    Task<bool> NeedsInitialSetupAsync(CancellationToken ct = default);

    Task CreateInitialAdminAsync(string username, string password, CancellationToken ct = default);

    Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}
