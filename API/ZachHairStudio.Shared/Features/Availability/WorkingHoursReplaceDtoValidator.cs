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

        RuleFor(x => x.Segments)
            .Must(NotHaveOverlappingSegmentsOnSameDay)
            .WithMessage("Segments for the same day must not overlap.");
    }

    private static bool BeAlignedToGrid(TimeOnly time) =>
        time.Minute % GridMinutes == 0 && time.Second == 0 && time.Millisecond == 0;

    /// <summary>
    /// Rejects two-or-more segments sharing a DayOfWeek whose [StartTime, EndTime)
    /// ranges overlap (or are exact duplicates) — the client's mergeSegments
    /// already prevents this, but the server must never trust the client for it
    /// (see class doc comment).
    /// </summary>
    private static bool NotHaveOverlappingSegmentsOnSameDay(List<WorkingHoursSegmentDto> segments)
    {
        foreach (var daySegments in segments.GroupBy(segment => segment.DayOfWeek))
        {
            var ordered = daySegments.OrderBy(segment => segment.StartTime).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].StartTime < ordered[i - 1].EndTime)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
