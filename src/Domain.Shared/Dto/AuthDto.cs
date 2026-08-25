namespace ResellerSystem.Domain.Shared.Dto;

public sealed class LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }

    /// <summary>When true, the server issues a long-lived session (see
    /// SessionService.RememberMeLifetime) so the client can persist the
    /// token as a "trusted device" and skip the password on next launch.</summary>
    public bool RememberMe { get; init; }
}

public sealed class LoginResponse
{
    public required string Token { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class InitialSetupRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed class AuthStatusResponse
{
    public required bool NeedsInitialSetup { get; init; }
}

public sealed class ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}
