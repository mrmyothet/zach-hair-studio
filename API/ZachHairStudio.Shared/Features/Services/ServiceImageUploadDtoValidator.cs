using FluentValidation;

namespace ZachHairStudio.Shared.Features.Services;

public class ServiceImageUploadDtoValidator : AbstractValidator<ServiceImageUploadDto>
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public ServiceImageUploadDtoValidator()
    {
        RuleFor(x => x.Image)
            .NotNull()
            .WithMessage("An image file is required.");

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image.Length)
                .LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage("Image must be 5MB or smaller.");

            RuleFor(x => x.Image.ContentType)
                .Must(contentType => AllowedContentTypes.Contains(contentType))
                .WithMessage("Image must be a JPEG, PNG, or WebP file.");
        });
    }
}
