using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games.Interfaces;
using GifJam.Api.Features.Games.Services;

namespace GifJam.Api.Features.Games;

/// <summary>
/// Compatibility facade for the public game service contract.
/// Lobby implementation lives in Features.Games.Services.GameLobbyService.
/// </summary>
public sealed class GameService(GameLobbyService lobbyService) : IGameService
{
    public Task<PlayerGameSnapshot> CreateAsync(Guid userId, int totalRounds, CancellationToken cancellationToken,
        int phraseSubmissionSeconds = 60, int resultsSeconds = 60, GameMode mode = GameMode.Classic,
        bool hostIsConnected = true) =>
        lobbyService.CreateAsync(
            userId,
            totalRounds,
            cancellationToken,
            phraseSubmissionSeconds,
            resultsSeconds,
            mode,
            hostIsConnected);

    public Task<PlayerGameSnapshot> CreateLobbyWithPlayersAsync(
        IReadOnlyList<Guid> userIds,
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds,
        GameMode mode,
        CancellationToken cancellationToken) =>
        lobbyService.CreateLobbyWithPlayersAsync(
            userIds,
            totalRounds,
            phraseSubmissionSeconds,
            resultsSeconds,
            mode,
            cancellationToken);

    public Task<PlayerGameSnapshot> JoinAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.JoinAsync(code, userId, cancellationToken);

    public Task LeaveAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.LeaveAsync(code, userId, cancellationToken);

    public Task CloseAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.CloseAsync(code, userId, cancellationToken);

    public Task<PlayerGameSnapshot> GetAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.GetAsync(code, userId, cancellationToken);

    public Task<PlayerGameSnapshot> ConnectAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.ConnectAsync(code, userId, cancellationToken);

    public Task DisconnectAsync(string code, Guid userId, CancellationToken cancellationToken) =>
        lobbyService.DisconnectAsync(code, userId, cancellationToken);

    public Task<LobbySnapshot> SetReadyAsync(string code, Guid userId, bool isReady, CancellationToken cancellationToken) =>
        lobbyService.SetReadyAsync(code, userId, isReady, cancellationToken);

    public Task<LobbySnapshot> SetVisibilityAsync(
        string code,
        Guid userId,
        RoomVisibility visibility,
        CancellationToken cancellationToken) =>
        lobbyService.SetVisibilityAsync(code, userId, visibility, cancellationToken);

    public Task<LobbySnapshot> UpdateSettingsAsync(string code, Guid userId, int totalRounds,
        int phraseSubmissionSeconds, int resultsSeconds, CancellationToken cancellationToken,
        GameMode mode = GameMode.Classic) =>
        lobbyService.UpdateSettingsAsync(code, userId, totalRounds, phraseSubmissionSeconds, resultsSeconds, cancellationToken, mode);
}
