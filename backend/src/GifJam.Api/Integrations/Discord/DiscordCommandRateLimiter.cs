using System.Threading.RateLimiting;

namespace GifJam.Api.Integrations.Discord;

public sealed class DiscordCommandRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter =
        PartitionedRateLimiter.Create<string, string>(discordUserId =>
            RateLimitPartition.GetFixedWindowLimiter(
                discordUserId,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public async ValueTask<bool> TryAcquireAsync(
        string discordUserId,
        CancellationToken cancellationToken)
    {
        using var lease = await limiter.AcquireAsync(discordUserId, 1, cancellationToken);
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();
}
