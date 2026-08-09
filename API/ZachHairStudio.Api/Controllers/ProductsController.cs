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

    [HttpGet("{slug}", Name = nameof(GetProduct))]
    public async Task<ActionResult<ProductResponseDto>> GetProduct(string slug)
    {
        var result = await _productsService.GetBySlugAsync(slug);
        return result.IsSuccess ? Ok(result.Data) : NotFound();
    }
}
