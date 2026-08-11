namespace ZachHairStudio.Api;

/// <summary>
/// LAUNCH-02 helpers — kept testable without booting a Production host against SQL.
/// </summary>
public static class CorsOrigins
{
    public static string[] Parse(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
