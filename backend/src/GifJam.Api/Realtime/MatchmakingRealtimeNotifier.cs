using GifJam.Api.Features.Matchmaking;
using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

public sealed class MatchmakingRealtimeNotifier(
    IHubContext<GameHub, IGameClient> hubContext) : IMatchmakingRealtimeNotifier
{
    public Task MatchmakingUpdatedAsync(
        Guid userId,
        MatchmakingSnapshot snapshot,
        CancellationToken cancellationToken) =>
        hubContext.Clients.User(userId.ToString()).MatchmakingUpdated(snapshot).WaitAsync(cancellationToken);

    public Task MatchFoundAsync(
        Guid userId,
        MatchFoundSnapshot snapshot,
        CancellationToken cancellationToken) =>
        hubContext.Clients.User(userId.ToString()).MatchFound(snapshot).WaitAsync(cancellationToken);
}
