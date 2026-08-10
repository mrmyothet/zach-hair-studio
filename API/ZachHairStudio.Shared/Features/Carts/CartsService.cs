using Microsoft.EntityFrameworkCore;
using ZachHairStudio.Shared.Db;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Shared.Features.Carts;

// This class owns ALL Cart/CartItem BookingDbContext access (PLAT-01).
public class CartsService
{
    private readonly BookingDbContext _dbContext;

    public CartsService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<CartResponseDto>> GetCartAsync(string sessionKey)
    {
        var cart = await FindCartWithItemsAsync(sessionKey);
        if (cart is null)
        {
            return Result<CartResponseDto>.Success(new CartResponseDto
            {
                SessionKey = sessionKey,
                Items = [],
                Subtotal = 0m,
            });
        }

        return Result<CartResponseDto>.Success(await ToEnrichedResponseAsync(cart));
    }

    public async Task<Result<CartResponseDto>> UpsertItemAsync(string sessionKey, CartItemUpsertDto dto)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

        if (product is null)
        {
            return Result<CartResponseDto>.NotFoundError($"Product '{dto.ProductId}' not found.");
        }

        if (product.Stock < 1)
        {
            return Result<CartResponseDto>.ValidationError(
                $"Sorry, {product.Name} is out of stock.");
        }

        var quantity = Math.Min(dto.Quantity, product.Stock);

        var cart = await GetOrCreateCartAsync(sessionKey);
        var existing = cart.Items.FirstOrDefault(item => item.ProductId == dto.ProductId);
        if (existing is null)
        {
            cart.Items.Add(new CartItem
            {
                ProductId = dto.ProductId,
                Quantity = quantity,
            });
        }
        else
        {
            existing.Quantity = quantity;
        }

        await _dbContext.SaveChangesAsync();

        // Reload navigation for enrichment after insert.
        await _dbContext.Entry(cart).Collection(c => c.Items).LoadAsync();
        return Result<CartResponseDto>.Success(await ToEnrichedResponseAsync(cart));
    }

    public async Task<Result<CartResponseDto>> RemoveItemAsync(string sessionKey, int productId)
    {
        var cart = await FindCartWithItemsAsync(sessionKey);
        if (cart is null)
        {
            return Result<CartResponseDto>.Success(new CartResponseDto
            {
                SessionKey = sessionKey,
                Items = [],
                Subtotal = 0m,
            });
        }

        var line = cart.Items.FirstOrDefault(item => item.ProductId == productId);
        if (line is not null)
        {
            _dbContext.CartItems.Remove(line);
            await _dbContext.SaveChangesAsync();
            cart.Items.Remove(line);
        }

        return Result<CartResponseDto>.Success(await ToEnrichedResponseAsync(cart));
    }

    private async Task<Cart> GetOrCreateCartAsync(string sessionKey)
    {
        var cart = await FindCartWithItemsAsync(sessionKey);
        if (cart is not null)
        {
            return cart;
        }

        cart = new Cart { SessionKey = sessionKey };
        _dbContext.Carts.Add(cart);
        await _dbContext.SaveChangesAsync();
        return cart;
    }

    private Task<Cart?> FindCartWithItemsAsync(string sessionKey)
        => _dbContext.Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.SessionKey == sessionKey);

    private async Task<CartResponseDto> ToEnrichedResponseAsync(Cart cart)
    {
        var productIds = cart.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id);

        return cart.ToResponseDto(products);
    }
}
