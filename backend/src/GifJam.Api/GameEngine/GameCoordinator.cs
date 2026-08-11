using GifJam.Api.Features.Games;
using GifJam.Api.Features.Games.Interfaces;

namespace GifJam.Api.GameEngine;

/// <summary>
/// Compatibility facade kept for realtime and test callers.
/// Round orchestration lives in Features.Games.Services.GameRoundService.
/// </summary>
public sealed class GameCoordinator(IGameRoundService roundService) : IGameRoundService
{
    public Task<PlayerGameSnapshot> StartGameAsync(string gameCode, Guid userId, CancellationToken cancellationToken) =>
        roundService.StartGameAsync(gameCode, userId, cancellationToken);

    public Task<PlayerGameSnapshot> SubmitPhraseAsync(string gameCode, Guid userId, string text, CancellationToken cancellationToken) =>
        roundService.SubmitPhraseAsync(gameCode, userId, text, cancellationToken);

    public Task<PlayerGameSnapshot> VotePhraseAsync(string gameCode, Guid userId, Guid phraseId, CancellationToken cancellationToken) =>
        roundService.VotePhraseAsync(gameCode, userId, phraseId, cancellationToken);

    public Task<PlayerGameSnapshot> SubmitGifAsync(string gameCode, Guid userId, string selectionToken, CancellationToken cancellationToken) =>
        roundService.SubmitGifAsync(gameCode, userId, selectionToken, cancellationToken);

    public Task<PlayerGameSnapshot> VoteGifAsync(string gameCode, Guid userId, Guid gifSubmissionId, CancellationToken cancellationToken) =>
        roundService.VoteGifAsync(gameCode, userId, gifSubmissionId, cancellationToken);

    public Task<PlayerGameSnapshot> SetResultsReadyAsync(string gameCode, Guid userId, CancellationToken cancellationToken) =>
        roundService.SetResultsReadyAsync(gameCode, userId, cancellationToken);

    public Task ProcessExpiredRoundsAsync(CancellationToken cancellationToken) =>
        roundService.ProcessExpiredRoundsAsync(cancellationToken);
}
