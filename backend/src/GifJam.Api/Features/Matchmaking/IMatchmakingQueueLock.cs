namespace GifJam.Api.Features.Matchmaking;

public interface IMatchmakingQueueLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
