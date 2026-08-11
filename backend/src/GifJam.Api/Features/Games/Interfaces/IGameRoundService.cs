using GifJam.Api.Features.Games;

namespace GifJam.Api.Features.Games.Interfaces;

public interface IGameRoundService
{
    Task<PlayerGameSnapshot> StartGameAsync(string gameCode, Guid userId, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> SubmitPhraseAsync(string gameCode, Guid userId, string text, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> VotePhraseAsync(string gameCode, Guid userId, Guid phraseId, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> SubmitGifAsync(string gameCode, Guid userId, string selectionToken, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> VoteGifAsync(string gameCode, Guid userId, Guid gifSubmissionId, CancellationToken cancellationToken);

    Task<PlayerGameSnapshot> SetResultsReadyAsync(string gameCode, Guid userId, CancellationToken cancellationToken);

    Task ProcessExpiredRoundsAsync(CancellationToken cancellationToken);
}
