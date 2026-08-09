using FluentValidation.TestHelper;
using ZachHairStudio.Shared.Features.Products;

namespace ZachHairStudio.Api.Tests.Features.Products;

public class ProductCreateDtoValidatorTests
{
    private readonly ProductCreateDtoValidator _validator = new();

    [Fact]
    public void Validate_WhenNameIsEmpty_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Name = string.Empty;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenNameExceedsMaximumLength_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Name = new string('a', 151);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenPriceIsNegative_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Price = -1m;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Validate_WhenPriceIsZero_DoesNotHaveValidationError()
    {
        var dto = CreateValidDto();
        dto.Price = 0m;

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Validate_WhenStockIsNegative_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Stock = -1;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Stock);
    }

    [Fact]
    public void Validate_WhenStockIsZero_DoesNotHaveValidationError()
    {
        var dto = CreateValidDto();
        dto.Stock = 0;

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Stock);
    }

    [Theory]
    [InlineData("Leave In Serum")]
    [InlineData("leave in serum")]
    [InlineData("Leave-In-Serum")]
    public void Validate_WhenSlugIsNotLowercaseKebabCase_HasValidationError(string slug)
    {
        var dto = CreateValidDto();
        dto.Slug = slug;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_WhenSlugIsLowercaseKebabCase_DoesNotHaveValidationError()
    {
        var dto = CreateValidDto();
        dto.Slug = "leave-in-repair-serum";

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_WhenSlugExceedsMaximumLength_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Slug = "a-" + new string('a', 150);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_WhenShortDescriptionIsEmpty_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.ShortDescription = string.Empty;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.ShortDescription);
    }

    [Fact]
    public void Validate_WhenShortDescriptionExceedsMaximumLength_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.ShortDescription = new string('a', 201);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.ShortDescription);
    }

    [Fact]
    public void Validate_WhenLongDescriptionIsEmpty_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.LongDescription = string.Empty;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.LongDescription);
    }

    [Fact]
    public void Validate_WhenLongDescriptionExceedsMaximumLength_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.LongDescription = new string('a', 2001);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.LongDescription);
    }

    [Fact]
    public void Validate_WhenCategoryIsEmpty_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Category = string.Empty;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void Validate_WhenCategoryExceedsMaximumLength_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.Category = new string('a', 51);

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void Validate_WhenDtoIsValid_DoesNotHaveValidationErrors()
    {
        var dto = CreateValidDto();

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static ProductCreateDto CreateValidDto()
        => new()
        {
            Slug = "leave-in-repair-serum",
            Name = "Leave-In Repair Serum",
            ShortDescription = "A lightweight leave-in serum that locks in smoothness.",
            LongDescription = "A lightweight leave-in serum formulated to extend the smoothing effects of a keratin treatment between salon visits.",
            Category = "Hair Care",
            Price = 24.00m,
            Stock = 40,
            ImageUrl = "/images/products/leave-in-repair-serum.jpg",
        };
}
