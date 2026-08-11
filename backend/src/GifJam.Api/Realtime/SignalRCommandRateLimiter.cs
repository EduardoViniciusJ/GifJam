using System.Threading.RateLimiting;

namespace GifJam.Api.Realtime;

public sealed class SignalRCommandRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter =
        PartitionedRateLimiter.Create<string, string>(userId =>
            RateLimitPartition.GetSlidingWindowLimiter(
                userId,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public ValueTask<RateLimitLease> AcquireAsync(
        string userId,
        CancellationToken cancellationToken) =>
        limiter.AcquireAsync(userId, 1, cancellationToken);

    public void Dispose() => limiter.Dispose();
}
