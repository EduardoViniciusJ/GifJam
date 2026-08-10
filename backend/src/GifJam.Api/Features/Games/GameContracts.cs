using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Games;

public sealed record CreateGameRequest(int TotalRounds);

public sealed record LobbyPlayerSnapshot(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int Score,
    bool IsReady,
    bool IsConnected,
    bool IsHost);

public sealed record LobbySnapshot(
    string Code,
    GameStatus Status,
    int TotalRounds,
    int CurrentRoundNumber,
    Guid HostUserId,
    bool CanStart,
    IReadOnlyList<LobbyPlayerSnapshot> Players,
    DateTimeOffset ServerTime);

public sealed record PlayerGameSnapshot(LobbySnapshot Lobby, bool IsHost);

public sealed record PresencePlayerSnapshot(Guid UserId, bool IsConnected);

public sealed record PresenceSnapshot(
    string Code,
    IReadOnlyList<PresencePlayerSnapshot> Players,
    DateTimeOffset ServerTime);
