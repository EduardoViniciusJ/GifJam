using GifJam.Api.Common.Errors;
using GifJam.Api.Data;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Domain.Rules;
using GifJam.Api.Features.Games;
using GifJam.Api.Realtime;

namespace GifJam.Api.Features.Games.Services;

public sealed partial class GameRoundService
{
    public async Task<PlayerGameSnapshot> StartGameAsync(
        string gameCode,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);

        if (game.HostUserId != userId)
        {
            throw new ApiException("host_required", "Only the host can start the game.", StatusCodes.Status403Forbidden);
        }

        if (game.Status != GameStatus.Lobby)
        {
            throw new ApiException("game_already_started", "The game has already started.", StatusCodes.Status409Conflict);
        }

        if (game.Players.Count is < GameRules.MinimumPlayers or > GameRules.MaximumPlayers ||
            game.Players.Any(player => player.UserId != userId && !player.IsReady))
        {
            throw new ApiException(
                "lobby_not_ready",
                "The game requires 2 to 6 players and every guest must be ready.",
                StatusCodes.Status409Conflict);
        }

        var now = clock.UtcNow;
        var round = await CreateRoundAsync(game, 1, cancellationToken);
        game.Status = GameStatus.InProgress;
        game.StartedAt = now;
        game.CurrentRoundNumber = 1;
        game.Version++;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        gameTelemetry.GameStarted(game.Code, game.Players.Count, game.TotalRounds);
        await PublishPhaseAsync(game, round);
        await realtimeNotifier.LobbyUpdatedAsync(
            game.Code,
            stateProjector.CreateLobbySnapshot(game),
            CancellationToken.None);
        return stateProjector.CreatePlayerSnapshot(game, userId);
    }
}
