namespace ZachHairStudio.Shared.Features.Account;

/// <summary>Guest history matches available to claim after register (D-04).</summary>
public class ClaimPreviewDto
{
    public List<ClaimAppointmentSummaryDto> Appointments { get; set; } = [];

    public List<ClaimOrderSummaryDto> Orders { get; set; } = [];
}

public class ClaimAppointmentSummaryDto
{
    public int Id { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public string ServiceName { get; set; } = null!;

    public string Status { get; set; } = null!;
}

public class ClaimOrderSummaryDto
{
    public int Id { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public int ItemCount { get; set; }
}
