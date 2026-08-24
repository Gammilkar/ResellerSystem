using FluentAssertions;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Databases.Validators;
using Xunit;

namespace ResellerSystem.Server.Application.Tests.Databases;

public class CreateDatabaseRequestValidatorTests
{
    private readonly CreateDatabaseRequestValidator _validator =
        new(new FakeTimeZoneValidator(), new FakeCurrencyValidator());

    [Fact]
    public void Empty_name_is_invalid()
    {
        var result = _validator.Validate(new CreateDatabaseRequest { Name = "", TimeZone = "UTC", Currency = "USD" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatabaseRequest.Name));
    }

    [Fact]
    public void Blank_name_is_invalid()
    {
        var result = _validator.Validate(new CreateDatabaseRequest { Name = "   ", TimeZone = "UTC", Currency = "USD" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Name_over_max_length_is_invalid()
    {
        var longName = new string('a', CreateDatabaseRequestValidator.MaxNameLength + 1);

        var result = _validator.Validate(new CreateDatabaseRequest { Name = longName, TimeZone = "UTC", Currency = "USD" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Unrecognized_timezone_is_invalid()
    {
        var result = _validator.Validate(new CreateDatabaseRequest { Name = "Test", TimeZone = "Pacific Standard Timezzz", Currency = "USD" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatabaseRequest.TimeZone));
    }

    [Fact]
    public void Unrecognized_currency_is_invalid()
    {
        var result = _validator.Validate(new CreateDatabaseRequest { Name = "Test", TimeZone = "UTC", Currency = "XXX" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateDatabaseRequest.Currency));
    }

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(new CreateDatabaseRequest { Name = "Main Business", TimeZone = "America/Los_Angeles", Currency = "USD" });

        result.IsValid.Should().BeTrue();
    }
}

public class UpdateDatabaseRequestValidatorTests
{
    private readonly UpdateDatabaseRequestValidator _validator = new(new FakeTimeZoneValidator());

    [Fact]
    public void Null_fields_are_valid_partial_update()
    {
        var result = _validator.Validate(new UpdateDatabaseRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Blank_name_is_invalid_when_provided()
    {
        var result = _validator.Validate(new UpdateDatabaseRequest { Name = "   " });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Invalid_timezone_is_rejected_when_provided()
    {
        var result = _validator.Validate(new UpdateDatabaseRequest { TimeZone = "Not/AZone" });

        result.IsValid.Should().BeFalse();
    }
}
