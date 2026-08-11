using GifJam.Api.Domain.Entities;

namespace GifJam.Api.Data.Repositories;

public interface IGameRepository
{
    Task<Guid?> FindIdAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<Game> LoadAsync(Guid gameId, CancellationToken cancellationToken, bool tracking = true);
}
