using FluentValidation;

namespace ZachHairStudio.Shared.Features.Availability;

/// <summary>
/// Server-authoritative revalidation of every submitted segment (T-04-07) — the
/// client is never trusted for End &gt; Start or 15-minute grid alignment (matches
/// SlotService's fixed GridMinutes = 15).
/// </summary>
public class WorkingHoursReplaceDtoValidator : AbstractValidator<WorkingHoursReplaceDto>
{
    private const int GridMinutes = 15;

    public WorkingHoursReplaceDtoValidator()
    {
        RuleForEach(x => x.Segments).ChildRules(segment =>
        {
            segment.RuleFor(s => s.EndTime)
                .GreaterThan(s => s.StartTime)
                .WithMessage("EndTime must be after StartTime.");

            segment.RuleFor(s => s.StartTime)
                .Must(BeAlignedToGrid)
                .WithMessage($"StartTime must align to a {GridMinutes}-minute grid.");

            segment.RuleFor(s => s.EndTime)
                .Must(BeAlignedToGrid)
                .WithMessage($"EndTime must align to a {GridMinutes}-minute grid.");
        });
    }

    private static bool BeAlignedToGrid(TimeOnly time) =>
        time.Minute % GridMinutes == 0 && time.Second == 0 && time.Millisecond == 0;
}
