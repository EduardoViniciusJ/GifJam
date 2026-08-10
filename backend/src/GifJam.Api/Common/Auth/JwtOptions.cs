namespace GifJam.Api.Common.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "GifJam.Api";

    public string Audience { get; set; } = "GifJam.Web";

    public int LifetimeHours { get; set; } = 8;
}
