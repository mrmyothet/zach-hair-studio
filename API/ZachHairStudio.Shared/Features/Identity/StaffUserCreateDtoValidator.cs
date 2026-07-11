using FluentValidation;

namespace ZachHairStudio.Shared.Features.Identity;

public class StaffUserCreateDtoValidator : AbstractValidator<StaffUserCreateDto>
{
    public StaffUserCreateDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
