using FluentValidation;

namespace ZachHairStudio.Shared.Features.Appointments;

/// <summary>
/// PLAT-02 — StartsAt rules aligned with <see cref="AppointmentCreateDtoValidator"/>
/// (future / 15-minute grid / 60-day horizon). Contact fields are not on this DTO.
/// </summary>
public class ClientRescheduleRequestDtoValidator : AbstractValidator<ClientRescheduleRequestDto>
{
    private static readonly TimeSpan BookingHorizon = TimeSpan.FromDays(60);

    public ClientRescheduleRequestDtoValidator()
    {
        RuleFor(x => x.StylistId)
            .GreaterThan(0)
            .When(x => x.StylistId.HasValue)
            .WithMessage("StylistId must be a positive stylist identifier.");

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
