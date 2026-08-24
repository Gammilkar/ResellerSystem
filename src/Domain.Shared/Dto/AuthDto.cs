namespace ResellerSystem.Domain.Shared.Dto;

public sealed class LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
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
