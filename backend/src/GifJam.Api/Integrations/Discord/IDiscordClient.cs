namespace GifJam.Api.Integrations.Discord;

public interface IDiscordClient
{
    Task<DiscordIdentity> GetIdentityAsync(string authorizationCode, CancellationToken cancellationToken);
}

public sealed record DiscordIdentity(
    string DiscordId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
