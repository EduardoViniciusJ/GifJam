namespace GifJam.Api.Integrations.Giphy;

public sealed class GiphyOptions
{
    public const string SectionName = "Giphy";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.giphy.com";

    public string Language { get; set; } = "pt";

    public string Rating { get; set; } = "g";

    public string Bundle { get; set; } = "messaging_non_clips";
}
