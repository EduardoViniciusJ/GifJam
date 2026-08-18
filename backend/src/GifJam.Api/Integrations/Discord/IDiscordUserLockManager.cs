namespace GifJam.Api.Integrations.Discord;

public interface IDiscordUserLockManager
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string discordUserId,
        CancellationToken cancellationToken = default);
}
