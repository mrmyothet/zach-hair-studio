using FluentValidation.TestHelper;
using ZachHairStudio.Shared.Features.Services;

namespace ZachHairStudio.Api.Tests.Features.Services;

public class ServiceCreateDtoValidatorTests
{
    private readonly ServiceCreateDtoValidator _validator = new();

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

    [Theory]
    [InlineData("Precision Cut")]
    [InlineData("precision cut")]
    [InlineData("Precision-Cut")]
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
        dto.Slug = "precision-cut";

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenDurationIsNotPositive_HasValidationError(int durationMinutes)
    {
        var dto = CreateValidDto();
        dto.DurationMinutes = durationMinutes;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Theory]
    [InlineData(45)]
    [InlineData(480)]
    public void Validate_WhenDurationIsWithinRange_DoesNotHaveValidationError(int durationMinutes)
    {
        var dto = CreateValidDto();
        dto.DurationMinutes = durationMinutes;

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Fact]
    public void Validate_WhenDurationExceedsMaximum_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.DurationMinutes = 481;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.DurationMinutes);
    }

    [Theory]
    [InlineData("")]
    public void Validate_WhenShortDescriptionIsEmpty_HasValidationError(string shortDescription)
    {
        var dto = CreateValidDto();
        dto.ShortDescription = shortDescription;

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
    public void Validate_WhenDisplayOrderIsNegative_HasValidationError()
    {
        var dto = CreateValidDto();
        dto.DisplayOrder = -1;

        var result = _validator.TestValidate(dto);

        result.ShouldHaveValidationErrorFor(x => x.DisplayOrder);
    }

    [Fact]
    public void Validate_WhenDtoIsValid_DoesNotHaveValidationErrors()
    {
        var dto = CreateValidDto();

        var result = _validator.TestValidate(dto);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static ServiceCreateDto CreateValidDto()
        => new()
        {
            Slug = "precision-cut",
            Name = "Precision Cut",
            ShortDescription = "A tailored cut designed around your style.",
            LongDescription = "A consultation-led haircut with detailed shaping and finishing.",
            Category = "Cuts",
            DurationMinutes = 45,
            Price = 65m,
            ImageUrl = "/images/services/precision-cut.jpg",
            DisplayOrder = 1,
        };
}
