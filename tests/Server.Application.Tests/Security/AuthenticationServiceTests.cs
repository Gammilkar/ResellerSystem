using FluentAssertions;
using NSubstitute;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Domain.Entities;
using Xunit;

namespace ResellerSystem.Server.Application.Tests.Security;

public class AuthenticationServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ISessionService _sessionService = Substitute.For<ISessionService>();
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _sut = new AuthenticationService(_userRepository, _passwordHasher, _sessionService);
    }

    [Fact]
    public async Task LoginAsync_fails_generically_for_unknown_username()
    {
        _userRepository.GetByUsernameAsync("nobody", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _sut.LoginAsync("nobody", "whatever");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task LoginAsync_fails_generically_for_wrong_password_same_message_as_unknown_user()
    {
        var user = User.CreateNew("admin", "hash", "salt");
        _userRepository.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong", "hash", "salt").Returns(false);

        var result = await _sut.LoginAsync("admin", "wrong");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task LoginAsync_fails_for_inactive_user_even_with_correct_password()
    {
        var user = User.CreateNew("admin", "hash", "salt");
        user.SetActive(false);
        _userRepository.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync("admin", "correct");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_succeeds_and_returns_session_token_for_correct_credentials()
    {
        var user = User.CreateNew("admin", "hash", "salt");
        _userRepository.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct", "hash", "salt").Returns(true);

        var expiresAt = DateTimeOffset.UtcNow.AddHours(12);
        _sessionService.CreateSessionAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new SessionInfo("tok123", user.Id, expiresAt));

        var result = await _sut.LoginAsync("admin", "correct");

        result.Success.Should().BeTrue();
        result.Token.Should().Be("tok123");
        result.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task NeedsInitialSetupAsync_true_when_no_users_exist()
    {
        _userRepository.AnyUsersExistAsync(Arg.Any<CancellationToken>()).Returns(false);

        (await _sut.NeedsInitialSetupAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task CreateInitialAdminAsync_throws_ConflictException_if_a_user_already_exists()
    {
        _userRepository.AnyUsersExistAsync(Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _sut.CreateInitialAdminAsync("admin", "password123");

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateInitialAdminAsync_hashes_password_and_persists_user()
    {
        _userRepository.AnyUsersExistAsync(Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.Hash("password123").Returns(("hashed", "salted"));

        await _sut.CreateInitialAdminAsync("admin", "password123");

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Username == "admin" && u.PasswordHash == "hashed" && u.PasswordSalt == "salted"),
            Arg.Any<CancellationToken>());
    }
}
