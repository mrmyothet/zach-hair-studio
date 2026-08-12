namespace ZachHairStudio.Api.Features.Chat;

public sealed class HuggingFaceOptions
{
    public string Endpoint { get; set; } = "https://router.huggingface.co/hf-inference/v1";
    public string Model { get; set; } = "Qwen/Qwen2.5-7B-Instruct";
    public string ApiKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int MaxToolRounds { get; set; } = 5;
}
