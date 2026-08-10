namespace ZachHairStudio.Shared.Features.Account;

/// <summary>Confirm=true attaches email-matched guest rows; false is an explicit skip (D-04).</summary>
public class ClaimRequestDto
{
    public bool Confirm { get; set; }
}
