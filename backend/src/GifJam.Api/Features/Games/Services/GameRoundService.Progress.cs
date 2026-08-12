using GifJam.Api.Common.Time;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;

namespace GifJam.Api.Features.Games.Services;

public sealed partial class GameRoundService
{
    private SubmissionProgressSnapshot GetSubmissionProgress(
        Game game,
        Round round,
        Func<Round, Guid, bool> hasCompleted)
    {
        var eligiblePlayers = game.Players
            .Where(player => player.LeftAt is null && player.IsConnected)
            .Select(player => player.UserId)
            .ToArray();
        var completed = eligiblePlayers.Count(playerId => hasCompleted(round, playerId));
        return new(completed, eligiblePlayers.Length, clock.UtcNow);
    }

    private SubmissionProgressSnapshot GetGifVotingProgress(Game game, Round round)
    {
        var eligiblePlayers = game.Players
            .Where(player => player.LeftAt is null && player.IsConnected &&
                round.GifSubmissions.Any(submission => submission.UserId != player.UserId))
            .Select(player => player.UserId)
            .ToArray();
        var completed = eligiblePlayers.Count(playerId => round.GifVotes.Any(vote => vote.UserId == playerId));
        return new(completed, eligiblePlayers.Length, clock.UtcNow);
    }

    private SubmissionProgressSnapshot GetResultsReadyProgress(Game game, Round round)
    {
        var eligiblePlayers = game.Players
            .Where(player => player.LeftAt is null && player.IsConnected)
            .ToArray();
        var completed = eligiblePlayers.Count(
            player => player.ResultReadyRoundNumber == round.RoundNumber);
        return new(completed, eligiblePlayers.Length, clock.UtcNow);
    }

    private async Task PublishPhaseAsync(Game game, Round round) =>
        await PublishPhaseCoreAsync(game, round);

    private async Task PublishPhaseCoreAsync(Game game, Round round)
    {
        gameTelemetry.PhaseChanged(game.Code, round.RoundNumber, round.Phase);
        await realtimeNotifier.PhaseChangedAsync(
            game.Code,
            stateProjector.CreatePhaseSnapshot(round),
            CancellationToken.None);
    }

    private async Task PublishTransitionAsync(Game game, Round round)
    {
        await PublishPhaseAsync(game, round);
        if (round.Phase == RoundPhase.Results)
        {
            await realtimeNotifier.RoundRevealedAsync(
                game.Code,
                stateProjector.CreateRoundRevealSnapshot(round),
                CancellationToken.None);
            await realtimeNotifier.RankingUpdatedAsync(
                game.Code,
                stateProjector.CreateRankingSnapshot(game, isFinal: false),
                CancellationToken.None);
        }

        if (game.Status == GameStatus.Finished)
        {
            gameTelemetry.GameFinished(
                game.Code,
                game.TotalRounds,
                (game.FinishedAt ?? clock.UtcNow) - (game.StartedAt ?? game.CreatedAt));
            var ranking = stateProjector.CreateRankingSnapshot(game, isFinal: true);
            await realtimeNotifier.RankingUpdatedAsync(game.Code, ranking, CancellationToken.None);
            await realtimeNotifier.GameFinishedAsync(
                game.Code,
                new(game.Code, ranking, game.FinishedAt ?? clock.UtcNow, clock.UtcNow),
                CancellationToken.None);
        }
    }
}
