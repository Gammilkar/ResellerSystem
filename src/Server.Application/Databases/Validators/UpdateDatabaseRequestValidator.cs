using FluentValidation;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Application.Databases.Validators;

public sealed class UpdateDatabaseRequestValidator : AbstractValidator<UpdateDatabaseRequest>
{
    public UpdateDatabaseRequestValidator(ITimeZoneValidator timeZoneValidator)
    {
        RuleFor(x => x.Name)
            .Must(n => n is null || n.Trim().Length > 0)
                .WithMessage("Database name cannot be blank.")
            .Must(n => n is null || n.Trim().Length <= CreateDatabaseRequestValidator.MaxNameLength)
                .WithMessage($"Database name cannot exceed {CreateDatabaseRequestValidator.MaxNameLength} characters.");

        RuleFor(x => x.TimeZone)
            .Must(tz => tz is null || timeZoneValidator.TryNormalize(tz, out _))
            .WithMessage(x => $"'{x.TimeZone}' is not a recognized time zone identifier.");
    }
}
