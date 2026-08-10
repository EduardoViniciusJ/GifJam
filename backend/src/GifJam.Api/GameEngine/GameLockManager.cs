using System.Collections.Concurrent;

namespace GifJam.Api.GameEngine;

public sealed class GameLockManager : IGameLockManager
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var semaphore = locks.GetOrAdd(gameId, static _ => new(1, 1));
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
