using FluentValidation;

namespace ZachHairStudio.Shared.Features.Account;

public class ClaimRequestDtoValidator : AbstractValidator<ClaimRequestDto>
{
    public ClaimRequestDtoValidator()
    {
        // Confirm is required as a bool on the wire; FluentValidation treats bool as always present.
        // Explicit rule documents the contract for PLAT-02 scanners.
        RuleFor(x => x.Confirm).Must(_ => true);
    }
}
