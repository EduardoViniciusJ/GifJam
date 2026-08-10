namespace GifJam.Api.Common.Auth;

public sealed class ApplicationUrlOptions
{
    public const string SectionName = "ApplicationUrls";

    public string FrontendUrl { get; set; } = string.Empty;
}
