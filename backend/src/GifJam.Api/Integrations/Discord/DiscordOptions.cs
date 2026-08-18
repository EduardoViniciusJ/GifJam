namespace GifJam.Api.Integrations.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string CallbackUrl { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = "https://discord.com/oauth2/authorize";

    public string TokenEndpoint { get; set; } = "https://discord.com/api/oauth2/token";

    public string UserEndpoint { get; set; } = "https://discord.com/api/users/@me";

    public bool BotEnabled { get; set; }

    public string BotToken { get; set; } = string.Empty;

    public ulong? DevelopmentGuildId { get; set; }

    public string BotActivity { get; set; } = "GifJam";
}
