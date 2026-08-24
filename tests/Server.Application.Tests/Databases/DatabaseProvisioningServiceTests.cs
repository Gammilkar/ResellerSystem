using System.Text.RegularExpressions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Domain.Shared.Enums;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Databases.Validators;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Entities;
using ResellerSystem.Server.Domain.Enums;
using Xunit;

namespace ResellerSystem.Server.Application.Tests.Databases;

public class DatabaseProvisioningServiceTests
{
    private readonly IDatabaseProfileRepository _repository = Substitute.For<IDatabaseProfileRepository>();
    private readonly ITenantDatabaseProvisioner _provisioner = Substitute.For<ITenantDatabaseProvisioner>();
    private readonly FakeTimeZoneValidator _timeZoneValidator = new();
    private readonly IValidator<CreateDatabaseRequest> _createValidator;
    private readonly IValidator<UpdateDatabaseRequest> _updateValidator;
    private readonly DatabaseProvisioningService _sut;

    public DatabaseProvisioningServiceTests()
    {
        _createValidator = new CreateDatabaseRequestValidator(_timeZoneValidator, new FakeCurrencyValidator());
        _updateValidator = new UpdateDatabaseRequestValidator(_timeZoneValidator);

        _sut = new DatabaseProvisioningService(
            _repository,
            _provisioner,
            _timeZoneValidator,
            _createValidator,
            _updateValidator,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseProvisioningService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_generates_physical_name_from_sequence_not_from_display_name()
    {
        _repository.GetNextPhysicalSequenceAsync(Arg.Any<CancellationToken>()).Returns(15L);
        _repository.PhysicalNameExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _provisioner.DatabaseExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _provisioner.ApplyTenantMigrationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        var request = new CreateDatabaseRequest { Name = "Сергей", TimeZone = "UTC", Currency = "USD" };

        await _sut.CreateAsync(request);

        await _provisioner.Received(1).CreateDatabaseAsync(
            Arg.Is<string>(name => Regex.IsMatch(name, @"^reseller_db_\d{6}$") && name == "reseller_db_000015"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_registers_tenant_with_status_Creating_before_provisioning()
    {
        _repository.GetNextPhysicalSequenceAsync(Arg.Any<CancellationToken>()).Returns(1L);
        _provisioner.ApplyTenantMigrationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        // DatabaseProfile is mutated in place by MarkReady() later in the same
        // CreateAsync call, so Arg.Is checked after the fact would see the final
        // status, not the status at AddAsync-time. Capture it via a callback instead.
        DatabaseStatus? statusAtAddTime = null;
        _repository
            .When(r => r.AddAsync(Arg.Any<DatabaseProfile>(), Arg.Any<CancellationToken>()))
            .Do(call => statusAtAddTime = call.Arg<DatabaseProfile>().Status);

        var request = new CreateDatabaseRequest { Name = "Test", TimeZone = "UTC", Currency = "USD" };

        await _sut.CreateAsync(request);

        statusAtAddTime.Should().Be(DatabaseStatus.Creating);
    }

    [Fact]
    public async Task CreateAsync_marks_Ready_with_schema_version_on_success()
    {
        _repository.GetNextPhysicalSequenceAsync(Arg.Any<CancellationToken>()).Returns(1L);
        _provisioner.ApplyTenantMigrationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);

        var request = new CreateDatabaseRequest { Name = "Test", TimeZone = "UTC", Currency = "USD" };

        var result = await _sut.CreateAsync(request);

        result.Status.Should().Be(DatabaseStatusDto.Ready);
        await _repository.Received(1).UpdateAsync(
            Arg.Is<DatabaseProfile>(p => p.Status == DatabaseStatus.Ready && p.SchemaVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_marks_MigrationFailed_and_throws_when_migrations_fail()
    {
        _repository.GetNextPhysicalSequenceAsync(Arg.Any<CancellationToken>()).Returns(1L);
        _provisioner.ApplyTenantMigrationsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("boom"));

        var request = new CreateDatabaseRequest { Name = "Test", TimeZone = "UTC", Currency = "USD" };

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<DatabaseNotReadyException>();
        await _repository.Received(1).UpdateAsync(
            Arg.Is<DatabaseProfile>(p => p.Status == DatabaseStatus.MigrationFailed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_throws_ValidationFailedException_for_blank_name()
    {
        var request = new CreateDatabaseRequest { Name = "   ", TimeZone = "UTC", Currency = "USD" };

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        await _repository.DidNotReceive().AddAsync(Arg.Any<DatabaseProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_renames_without_touching_physical_database_name()
    {
        var profile = DatabaseProfile.CreateNew("Original", "reseller_db_000042", "UTC", "USD");
        profile.MarkReady(1);
        _repository.GetByIdAsync(profile.Id, Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _sut.UpdateAsync(profile.Id, new UpdateDatabaseRequest { Name = "SimonSaleStore" });

        result.Name.Should().Be("SimonSaleStore");
        profile.PhysicalDatabaseName.Should().Be("reseller_db_000042");
    }

    [Fact]
    public async Task UpdateAsync_throws_NotFoundException_for_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DatabaseProfile?)null);

        var act = async () => await _sut.UpdateAsync(Guid.NewGuid(), new UpdateDatabaseRequest { Name = "X" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAsync_throws_NotFoundException_for_unknown_id()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((DatabaseProfile?)null);

        var act = async () => await _sut.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
