using FluentValidation;

namespace ZachHairStudio.Shared.Features.Availability;

public class TimeOffCreateDtoValidator : AbstractValidator<TimeOffCreateDto>
{
    public TimeOffCreateDtoValidator()
    {
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt)
            .WithMessage("EndsAt must be after StartsAt.");

        RuleFor(x => x.Reason)
            .MaximumLength(200);
    }
}
