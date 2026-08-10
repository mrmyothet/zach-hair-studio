using Microsoft.AspNetCore.Mvc;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ProductsService _productsService;

    public ProductsController(ProductsService productsService)
    {
        _productsService = productsService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetProducts()
    {
        var products = await _productsService.GetProductsAsync();
        return Ok(products);
    }

    /// <summary>
    /// SHOP-07 — stylist-recommended add-ons for cart chips.
    /// Query: repeated productIds=1&amp;productIds=2 (ASP.NET model-binds to int[]).
    /// </summary>
    [HttpGet("recommended-for-checkout")]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetRecommendedForCheckout(
        [FromQuery] int[]? productIds)
    {
        var recommendations = await _productsService.GetRecommendedForCheckoutAsync(
            productIds ?? Array.Empty<int>());
        return Ok(recommendations);
    }

    [HttpGet("{slug}", Name = nameof(GetProduct))]
    public async Task<ActionResult<ProductResponseDto>> GetProduct(string slug)
    {
        var result = await _productsService.GetBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Data) : NotFound();
    }
}
