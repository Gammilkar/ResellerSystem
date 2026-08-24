using FluentValidation;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Application.Databases.Validators;

public sealed class CreateDatabaseRequestValidator : AbstractValidator<CreateDatabaseRequest>
{
    public const int MaxNameLength = 100;

    public CreateDatabaseRequestValidator(ITimeZoneValidator timeZoneValidator, ICurrencyValidator currencyValidator)
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Database name is required.")
            .Must(n => n.Trim().Length > 0).WithMessage("Database name cannot be blank.")
            .Must(n => n.Trim().Length <= MaxNameLength)
                .WithMessage($"Database name cannot exceed {MaxNameLength} characters.");

        RuleFor(x => x.TimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Time zone is required.")
            .Must(tz => timeZoneValidator.TryNormalize(tz, out _))
                .WithMessage(x => $"'{x.TimeZone}' is not a recognized time zone identifier.");

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Currency is required.")
            .Must(currencyValidator.IsValid)
                .WithMessage(x => $"'{x.Currency}' is not a recognized ISO 4217 currency code.");
    }
}
