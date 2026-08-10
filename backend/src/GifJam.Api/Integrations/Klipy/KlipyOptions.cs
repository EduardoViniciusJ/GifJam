namespace GifJam.Api.Integrations.Klipy;

public sealed class KlipyOptions
{
    public const string SectionName = "Klipy";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.klipy.com";

    public string Locale { get; set; } = "pt_BR";

    public string Country { get; set; } = "BR";

    public string ContentFilter { get; set; } = "high";
}
