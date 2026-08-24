using FluentAssertions;
using NSubstitute;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;
using ResellerSystem.Server.Domain.Entities;
using Xunit;

namespace ResellerSystem.Server.Application.Tests.Databases;

public class DatabaseContextResolverTests
{
    private readonly IDatabaseProfileRepository _repository = Substitute.For<IDatabaseProfileRepository>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly DatabaseContextResolver _sut;

    public DatabaseContextResolverTests()
    {
        _currentUser.CanAccessAllDatabases.Returns(true);
        _sut = new DatabaseContextResolver(_repository, _currentUser);
    }

    [Fact]
    public async Task ResolveAsync_throws_NotFound_for_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DatabaseProfile?)null);

        var act = async () => await _sut.ResolveAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ResolveAsync_throws_DatabaseNotReady_when_not_Ready()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000001", "UTC", "USD"); // still Creating
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        var act = async () => await _sut.ResolveAsync(profile.Id);

        await act.Should().ThrowAsync<DatabaseNotReadyException>();
    }

    [Fact]
    public async Task ResolveAsync_throws_DatabaseNotReady_when_inactive()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000002", "UTC", "USD");
        profile.MarkReady(1);
        profile.SetActive(false);
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        var act = async () => await _sut.ResolveAsync(profile.Id);

        await act.Should().ThrowAsync<DatabaseNotReadyException>();
    }

    [Fact]
    public async Task ResolveAsync_returns_context_for_ready_active_database_without_exposing_extra_info()
    {
        var profile = DatabaseProfile.CreateNew("Test", "reseller_db_000003", "UTC", "USD");
        profile.MarkReady(1);
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        var context = await _sut.ResolveAsync(profile.Id);

        context.DatabaseId.Should().Be(profile.Id);
        context.PhysicalDatabaseName.Should().Be("reseller_db_000003");
        context.DisplayName.Should().Be("Test");
    }
}
