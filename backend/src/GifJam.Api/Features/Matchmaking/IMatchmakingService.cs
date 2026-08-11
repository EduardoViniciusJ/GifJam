namespace GifJam.Api.Features.Matchmaking;

public interface IMatchmakingService
{
    Task<MatchmakingSnapshot> JoinAsync(Guid userId, CancellationToken cancellationToken);

    Task LeaveAsync(Guid userId, CancellationToken cancellationToken);

    Task<MatchmakingSnapshot> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task ProcessDueBatchesAsync(CancellationToken cancellationToken);
}
