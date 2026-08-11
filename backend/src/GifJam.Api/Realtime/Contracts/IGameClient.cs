using GifJam.Api.Features.Games;
using GifJam.Api.Features.Matchmaking;

namespace GifJam.Api.Realtime.Contracts;

public interface IGameClient
{
    Task StateSynced(PlayerGameSnapshot snapshot);

    Task LobbyUpdated(LobbySnapshot snapshot);

    Task PresenceChanged(PresenceSnapshot snapshot);

    Task PhaseChanged(RoundPhaseSnapshot snapshot);

    Task SubmissionProgress(SubmissionProgressSnapshot progress);

    Task RoundRevealed(RoundRevealSnapshot reveal);

    Task RankingUpdated(RankingSnapshot ranking);

    Task GameFinished(GameFinishedSnapshot game);

    Task CommandRejected(CommandRejectedMessage rejection);

    Task MatchmakingUpdated(MatchmakingSnapshot snapshot);

    Task MatchFound(MatchFoundSnapshot snapshot);
}

public sealed record CommandRejectedMessage(string Code, string Message, string? CurrentPhase = null);
