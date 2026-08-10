using FluentValidation;

namespace ZachHairStudio.Shared.Features.Carts;

/// <summary>
/// Field bounds for cart upsert. Stock cap is enforced in CartsService against
/// Products.Stock — validator only gates ProductId &gt; 0 and Quantity 1..99.
/// </summary>
public class CartItemUpsertDtoValidator : AbstractValidator<CartItemUpsertDto>
{
    public CartItemUpsertDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .GreaterThan(0);

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 99);
    }
}
