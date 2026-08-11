using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Matchmaking;

public enum MatchmakingStatus
{
    NotInQueue = 0,
    Waiting = 1,
    Matched = 2
}

public sealed record MatchmakingSnapshot(
    MatchmakingStatus Status,
    int PlayerCount,
    int MinimumPlayers,
    int MaximumPlayers,
    Guid? HostUserId,
    DateTimeOffset? DeadlineAt,
    string? GameCode,
    GameMode? GameMode,
    DateTimeOffset ServerTime);

public sealed record MatchFoundSnapshot(
    string GameCode,
    Guid HostUserId,
    int PlayerCount,
    DateTimeOffset ServerTime);
