using GifJam.Api.Data;
using GifJam.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.GameEngine;

public sealed partial class GameRecoveryService(
    AppDbContext dbContext,
    GameCoordinator gameCoordinator,
    ILogger<GameRecoveryService> logger)
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var disconnectedPlayers = await dbContext.GamePlayers
            .Where(player => player.IsConnected &&
                (player.Game.Status == GameStatus.Lobby || player.Game.Status == GameStatus.InProgress))
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(player => player.IsConnected, false),
                cancellationToken);
        dbContext.ChangeTracker.Clear();
        await gameCoordinator.ProcessExpiredRoundsAsync(cancellationToken);
        LogRecoveryCompleted(logger, disconnectedPlayers);
    }

    [LoggerMessage(
        EventId = 4100,
        Level = LogLevel.Information,
        Message = "Active games recovered; {DisconnectedPlayers} stale player connections cleared")]
    private static partial void LogRecoveryCompleted(ILogger logger, int disconnectedPlayers);
}
