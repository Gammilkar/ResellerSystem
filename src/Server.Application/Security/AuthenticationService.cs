using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Entities;

namespace ResellerSystem.Server.Application.Security;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionService _sessionService;

    public AuthenticationService(IUserRepository userRepository, IPasswordHasher passwordHasher, ISessionService sessionService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, ct);
        if (user is null || !user.IsActive)
        {
            // Same generic failure for "no such user" and "wrong password" —
            // never reveal which one to avoid username enumeration.
            return new LoginResult(false, null, null, "Invalid username or password.");
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            return new LoginResult(false, null, null, "Invalid username or password.");
        }

        var session = await _sessionService.CreateSessionAsync(user.Id, ct);
        return new LoginResult(true, session.Token, session.ExpiresAt, null);
    }

    public async Task<bool> NeedsInitialSetupAsync(CancellationToken ct = default) =>
        !await _userRepository.AnyUsersExistAsync(ct);

    public async Task CreateInitialAdminAsync(string username, string password, CancellationToken ct = default)
    {
        if (await _userRepository.AnyUsersExistAsync(ct))
        {
            throw new ConflictException("ADMIN_ALREADY_EXISTS", "An admin account already exists — initial setup can only run once.");
        }

        var (hash, salt) = _passwordHasher.Hash(password);
        var user = User.CreateNew(username, hash, salt);
        await _userRepository.AddAsync(user, ct);
    }
}
