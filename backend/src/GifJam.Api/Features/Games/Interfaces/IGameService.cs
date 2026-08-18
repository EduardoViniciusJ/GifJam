using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Games.Interfaces;

public interface IGameService
{
    Task<PlayerGameSnapshot> CreateAsync(Guid userId, int totalRounds, CancellationToken cancellationToken,
        int phraseSubmissionSeconds = 60, int resultsSeconds = 60, GameMode mode = GameMode.Classic,
        bool hostIsConnected = true);

    Task<PlayerGameSnapshot> CreateLobbyWithPlayersAsync(
        IReadOnlyList<Guid> userIds,
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds,
        GameMode mode,
        CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> JoinAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task LeaveAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task CloseAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> GetAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> ConnectAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task DisconnectAsync(string code, Guid userId, CancellationToken cancellationToken);

    Task<LobbySnapshot> SetReadyAsync(string code, Guid userId, bool isReady, CancellationToken cancellationToken);

    Task<LobbySnapshot> SetVisibilityAsync(
        string code,
        Guid userId,
        RoomVisibility visibility,
        CancellationToken cancellationToken);

    Task<LobbySnapshot> UpdateSettingsAsync(string code, Guid userId, int totalRounds,
        int phraseSubmissionSeconds, int resultsSeconds, CancellationToken cancellationToken,
        GameMode mode = GameMode.Classic);
}
