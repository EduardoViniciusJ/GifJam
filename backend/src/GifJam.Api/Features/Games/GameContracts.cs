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

public sealed record PlayerGameSnapshot(
    LobbySnapshot Lobby,
    bool IsHost,
    PlayerRoundSnapshot? Round = null);

public sealed record PresencePlayerSnapshot(Guid UserId, bool IsConnected);

public sealed record PresenceSnapshot(
    string Code,
    IReadOnlyList<PresencePlayerSnapshot> Players,
    DateTimeOffset ServerTime);

public sealed record AnonymousPhraseSnapshot(Guid Id, string Text);

public sealed record PlayerPhraseSnapshot(Guid Id, string Text, bool IsOwn);

public sealed record SelectedPhraseSnapshot(Guid Id, string Text);

public sealed record PlayerGifSelectionSnapshot(
    string ExternalId,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution);

public sealed record RoundPhaseSnapshot(
    int RoundNumber,
    RoundPhase Phase,
    DateTimeOffset PhaseEndsAt,
    IReadOnlyList<AnonymousPhraseSnapshot> Phrases,
    SelectedPhraseSnapshot? SelectedPhrase,
    DateTimeOffset ServerTime);

public sealed record PlayerRoundSnapshot(
    int RoundNumber,
    RoundPhase Phase,
    DateTimeOffset PhaseEndsAt,
    bool HasSubmittedPhrase,
    bool HasVotedPhrase,
    bool HasSubmittedGif,
    IReadOnlyList<PlayerPhraseSnapshot> Phrases,
    SelectedPhraseSnapshot? SelectedPhrase,
    PlayerGifSelectionSnapshot? GifSelection,
    DateTimeOffset ServerTime);

public sealed record SubmissionProgressSnapshot(
    int Completed,
    int Eligible,
    DateTimeOffset ServerTime);
