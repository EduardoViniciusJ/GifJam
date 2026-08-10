namespace GifJam.Api.GameEngine;

public interface IGameLockManager
{
    ValueTask<IAsyncDisposable> AcquireAsync(Guid gameId, CancellationToken cancellationToken = default);
}
