using FluentValidation;

namespace ZachHairStudio.Shared.Features.Identity;

public class RegisterRequestDtoValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Confirm password must match password.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));
    }
}
