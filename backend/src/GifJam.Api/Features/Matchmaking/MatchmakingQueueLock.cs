namespace GifJam.Api.Features.Matchmaking;

public sealed class MatchmakingQueueLock : IMatchmakingQueueLock, IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public void Dispose() => semaphore.Dispose();

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
