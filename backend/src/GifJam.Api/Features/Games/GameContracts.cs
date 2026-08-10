using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Games;

public sealed record CreateGameRequest(
    int TotalRounds,
    int PhraseSubmissionSeconds = 60,
    int ResultsSeconds = 60,
    GameMode Mode = GameMode.Classic);

public sealed record UpdateGameSettingsRequest(
    int TotalRounds,
    int PhraseSubmissionSeconds,
    int ResultsSeconds,
    GameMode Mode = GameMode.Classic);

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
    GameMode Mode,
    int TotalRounds,
    int PhraseSubmissionSeconds,
    int ResultsSeconds,
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

public sealed record AnonymousGifSnapshot(
    Guid Id,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution);

public sealed record PlayerGifSnapshot(
    Guid Id,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution,
    bool IsOwn);

public sealed record RevealedPlayerSnapshot(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record RevealedPhraseSnapshot(
    Guid Id,
    string Text,
    PhraseSource Source,
    RevealedPlayerSnapshot? Author);

public sealed record RevealedGifSnapshot(
    Guid Id,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution,
    RevealedPlayerSnapshot Author,
    int VoteCount,
    int Position);

public sealed record RoundRevealSnapshot(
    int RoundNumber,
    RevealedPhraseSnapshot? Phrase,
    IReadOnlyList<RevealedGifSnapshot> Gifs,
    DateTimeOffset ServerTime);

public sealed record RankingEntrySnapshot(
    int Position,
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int Score);

public sealed record RankingSnapshot(
    string GameCode,
    int CompletedRounds,
    bool IsFinal,
    IReadOnlyList<RankingEntrySnapshot> Entries,
    DateTimeOffset ServerTime);

public sealed record GlobalRankingEntrySnapshot(
    int Position,
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    int Score,
    bool IsCurrentUser);

public sealed record GlobalRankingSnapshot(
    IReadOnlyList<GlobalRankingEntrySnapshot> Entries,
    DateTimeOffset ServerTime);

public sealed record GameFinishedSnapshot(
    string GameCode,
    RankingSnapshot Ranking,
    DateTimeOffset FinishedAt,
    DateTimeOffset ServerTime);

public sealed record RoundPhaseSnapshot(
    int RoundNumber,
    RoundPhase Phase,
    DateTimeOffset PhaseEndsAt,
    DateTimeOffset? GifVotingPresentationEndsAt,
    IReadOnlyList<AnonymousPhraseSnapshot> Phrases,
    IReadOnlyList<AnonymousGifSnapshot> Gifs,
    SelectedPhraseSnapshot? SelectedPhrase,
    DateTimeOffset ServerTime);

public sealed record PlayerRoundSnapshot(
    int RoundNumber,
    RoundPhase Phase,
    DateTimeOffset PhaseEndsAt,
    DateTimeOffset? GifVotingPresentationEndsAt,
    bool HasSubmittedPhrase,
    bool HasVotedPhrase,
    bool HasSubmittedGif,
    bool HasVotedGif,
    bool HasConfirmedResults,
    IReadOnlyList<PlayerPhraseSnapshot> Phrases,
    IReadOnlyList<PlayerGifSnapshot> Gifs,
    SelectedPhraseSnapshot? SelectedPhrase,
    PlayerGifSelectionSnapshot? GifSelection,
    RoundRevealSnapshot? Reveal,
    RankingSnapshot? Ranking,
    DateTimeOffset ServerTime);

public sealed record SubmissionProgressSnapshot(
    int Completed,
    int Eligible,
    DateTimeOffset ServerTime);
