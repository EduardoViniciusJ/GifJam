namespace GifJam.Api.Integrations.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.5-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    public int TimeoutSeconds { get; set; } = 10;
}
