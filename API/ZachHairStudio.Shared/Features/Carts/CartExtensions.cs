using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Shared.Features.Carts;

public static class CartExtensions
{
    /// <summary>
    /// Maps a persisted cart plus catalog products into a response DTO.
    /// UnitPrice/LineTotal are taken from <see cref="Product.Price"/> only (D-05).
    /// Lines whose product is missing from the catalog map are skipped.
    /// </summary>
    public static CartResponseDto ToResponseDto(
        this Cart cart,
        IReadOnlyDictionary<int, Product> productsById)
    {
        var items = new List<CartItemResponseDto>();

        foreach (var line in cart.Items)
        {
            if (!productsById.TryGetValue(line.ProductId, out var product))
            {
                continue;
            }

            items.Add(new CartItemResponseDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSlug = product.Slug,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
                LineTotal = product.Price * line.Quantity,
                Stock = product.Stock,
            });
        }

        return new CartResponseDto
        {
            SessionKey = cart.SessionKey,
            Items = items,
            Subtotal = items.Sum(item => item.LineTotal),
        };
    }
}
