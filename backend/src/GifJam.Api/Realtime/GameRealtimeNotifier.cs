using GifJam.Api.Features.Games;
using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

public sealed class GameRealtimeNotifier(IHubContext<GameHub, IGameClient> hubContext) : IGameRealtimeNotifier
{
    public Task LobbyUpdatedAsync(
        string gameCode,
        LobbySnapshot snapshot,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).LobbyUpdated(snapshot).WaitAsync(cancellationToken);

    public Task PresenceChangedAsync(
        string gameCode,
        PresenceSnapshot snapshot,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).PresenceChanged(snapshot).WaitAsync(cancellationToken);

    public Task PhaseChangedAsync(
        string gameCode,
        RoundPhaseSnapshot snapshot,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).PhaseChanged(snapshot).WaitAsync(cancellationToken);

    public Task SubmissionProgressAsync(
        string gameCode,
        SubmissionProgressSnapshot progress,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).SubmissionProgress(progress).WaitAsync(cancellationToken);

    public Task RoundRevealedAsync(
        string gameCode,
        RoundRevealSnapshot reveal,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).RoundRevealed(reveal).WaitAsync(cancellationToken);

    public Task RankingUpdatedAsync(
        string gameCode,
        RankingSnapshot ranking,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).RankingUpdated(ranking).WaitAsync(cancellationToken);

    public Task GameFinishedAsync(
        string gameCode,
        GameFinishedSnapshot game,
        CancellationToken cancellationToken) =>
        hubContext.Clients.Group(GameGroups.ForCode(gameCode)).GameFinished(game).WaitAsync(cancellationToken);
}
