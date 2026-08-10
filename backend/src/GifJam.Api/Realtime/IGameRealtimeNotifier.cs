using GifJam.Api.Features.Games;

namespace GifJam.Api.Realtime;

public interface IGameRealtimeNotifier
{
    Task LobbyUpdatedAsync(string gameCode, LobbySnapshot snapshot, CancellationToken cancellationToken);

    Task PresenceChangedAsync(string gameCode, PresenceSnapshot snapshot, CancellationToken cancellationToken);

    Task PhaseChangedAsync(string gameCode, RoundPhaseSnapshot snapshot, CancellationToken cancellationToken);

    Task SubmissionProgressAsync(
        string gameCode,
        SubmissionProgressSnapshot progress,
        CancellationToken cancellationToken);

    Task RoundRevealedAsync(
        string gameCode,
        RoundRevealSnapshot reveal,
        CancellationToken cancellationToken);

    Task RankingUpdatedAsync(
        string gameCode,
        RankingSnapshot ranking,
        CancellationToken cancellationToken);

    Task GameFinishedAsync(
        string gameCode,
        GameFinishedSnapshot game,
        CancellationToken cancellationToken);
}
