using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Data.Repositories;

public sealed class GameRepository(AppDbContext dbContext) : IGameRepository
{
    public Task<Guid?> FindIdAsync(string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.Games
            .AsNoTracking()
            .Where(game => game.Code == normalizedCode && game.Status != GameStatus.Closed)
            .Select(game => (Guid?)game.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Game> LoadAsync(
        Guid gameId,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        var query = dbContext.Games
            .Include(game => game.Players)
            .ThenInclude(player => player.User)
            .Include(game => game.Rounds.OrderByDescending(round => round.RoundNumber).Take(1))
            .ThenInclude(round => round.Phrases)
            .ThenInclude(phrase => phrase.User)
            .Include(game => game.Rounds.OrderByDescending(round => round.RoundNumber).Take(1))
            .ThenInclude(round => round.PhraseVotes)
            .Include(game => game.Rounds.OrderByDescending(round => round.RoundNumber).Take(1))
            .ThenInclude(round => round.GifSubmissions)
            .ThenInclude(submission => submission.User)
            .Include(game => game.Rounds.OrderByDescending(round => round.RoundNumber).Take(1))
            .ThenInclude(round => round.GifVotes)
            .Where(game => game.Id == gameId)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleAsync(cancellationToken);
    }
}
