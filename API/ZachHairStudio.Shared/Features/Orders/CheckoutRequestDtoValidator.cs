using FluentValidation;

namespace ZachHairStudio.Shared.Features.Orders;

public class CheckoutRequestDtoValidator : AbstractValidator<CheckoutRequestDto>
{
    public CheckoutRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(x => x.Name)
            .MaximumLength(200);

        RuleFor(x => x.SessionKey)
            .MaximumLength(64)
            .When(x => x.SessionKey is not null);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one checkout line is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.ProductId).GreaterThan(0);
            item.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        });

        RuleFor(x => x.RedeemPoints)
            .GreaterThanOrEqualTo(0)
            .Must(points => points!.Value % 10 == 0)
            .WithMessage("RedeemPoints must be a multiple of 10.")
            .When(x => x.RedeemPoints.HasValue);
    }
}
