namespace GifJam.Api.Features.Matchmaking;

public interface IMatchmakingRealtimeNotifier
{
    Task MatchmakingUpdatedAsync(
        Guid userId,
        MatchmakingSnapshot snapshot,
        CancellationToken cancellationToken);

    Task MatchFoundAsync(
        Guid userId,
        MatchFoundSnapshot snapshot,
        CancellationToken cancellationToken);
}
