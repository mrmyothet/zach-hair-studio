using FluentValidation;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// Validates the guest-booking create contract. Field bounds mirror the Appointment
/// entity (100/100/150/30). The StartsAt rules enforce a future, on-15-minute-grid
/// instant within an owner-reviewable booking window.
/// </summary>
public class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
{
    // Owner-reviewable booking-window defaults (flagged for owner review in 02-04-SUMMARY):
    //   - No same-day / minimum-lead cutoff: any strictly-future instant is accepted.
    //   - Maximum horizon: 60 days ahead.
    private static readonly TimeSpan BookingHorizon = TimeSpan.FromDays(60);

    public AppointmentCreateDtoValidator()
    {
        RuleFor(x => x.ServiceId)
            .GreaterThan(0);

        RuleFor(x => x.StylistId)
            .GreaterThan(0)
            .When(x => x.StylistId.HasValue)
            .WithMessage("StylistId must be a positive stylist identifier.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.Phone)
            .MaximumLength(30);

        RuleFor(x => x.StartsAt)
            .Must(BeInTheFuture).WithMessage("StartsAt must be in the future.")
            .Must(BeOnFifteenMinuteGrid).WithMessage("StartsAt must fall on a 15-minute boundary with zero seconds.")
            .Must(BeWithinHorizon).WithMessage("StartsAt is beyond the booking horizon.");
    }

    private static bool BeInTheFuture(DateTimeOffset startsAt)
        => startsAt > DateTimeOffset.UtcNow;

    private static bool BeOnFifteenMinuteGrid(DateTimeOffset startsAt)
        => startsAt.Minute % 15 == 0 && startsAt.Second == 0 && startsAt.Millisecond == 0;

    private static bool BeWithinHorizon(DateTimeOffset startsAt)
        => startsAt <= DateTimeOffset.UtcNow.Add(BookingHorizon);
}
