using System.ComponentModel.DataAnnotations;

namespace ZachHairStudio.Api.Features.Chat;

public sealed class ChatRequest
{
    [Required, MinLength(1), MaxLength(40)]
    public List<ChatHistoryMessage> Messages { get; set; } = [];
}

public sealed class ChatHistoryMessage : IValidatableObject
{
    [Required]
    public string Role { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role is not ("user" or "assistant"))
        {
            yield return new ValidationResult(
                "Role must be 'user' or 'assistant'.",
                [nameof(Role)]);
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            yield return new ValidationResult(
                "Content cannot be blank.",
                [nameof(Content)]);
        }
    }
}

public sealed record ChatResponse(string Reply);
