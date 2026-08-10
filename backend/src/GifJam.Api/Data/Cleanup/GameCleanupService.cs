using GifJam.Api.Common.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Data.Cleanup;

public sealed partial class GameCleanupService(
    AppDbContext dbContext,
    IClock clock,
    IOptions<GameRetentionOptions> options,
    ILogger<GameCleanupService> logger)
{
    public async Task<int> DeleteExpiredGamesAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow.AddHours(-options.Value.RetentionHours);
        var deleted = await dbContext.Games
            .Where(game => game.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            LogDeletedGames(logger, deleted);
        }

        return deleted;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Deleted {Count} expired games")]
    private static partial void LogDeletedGames(ILogger logger, int count);
}
