using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Data.Repositories;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Domain.Rules;
using GifJam.Api.Features.AiPhrases;
using GifJam.Api.Features.Games;
using GifJam.Api.Features.Games.Interfaces;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Features.Rooms;
using GifJam.Api.Realtime;
using GifJam.Api.GameEngine;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Features.Games.Services;

public sealed partial class GameRoundService(
    AppDbContext dbContext,
    IGameRepository gameRepository,
    IGameLockManager lockManager,
    IRandomizer randomizer,
    IClock clock,
    GameStateProjector stateProjector,
    IGameRealtimeNotifier realtimeNotifier,
    IRoomDirectoryRealtimeNotifier roomDirectoryNotifier,
    GifSelectionTokenService gifSelectionTokenService,
    AiPhraseGenerationService aiPhraseGenerationService,
    GameTelemetry gameTelemetry) : IGameRoundService
{
    private static readonly TimeSpan PhraseVotingDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan GifSubmissionDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GifPresentationDurationPerItem = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GifVotingSelectionDuration = TimeSpan.FromSeconds(20);

    public async Task<PlayerGameSnapshot> SubmitPhraseAsync(
        string gameCode,
        Guid userId,
        string text,
        CancellationToken cancellationToken)
    {
        var normalizedText = text.Trim();
        if (normalizedText.Length is < 1 or > 180)
        {
            throw new ApiException(
                "invalid_phrase",
                "A phrase must contain between 1 and 180 characters.",
                StatusCodes.Status400BadRequest);
        }

        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        var player = EnsureMember(game, userId);
        var round = GetCurrentRound(game);
        EnsurePhase(round, RoundPhase.PhraseSubmission);
        EnsureBeforeDeadline(round);

        var existingPhrase = round.Phrases.SingleOrDefault(phrase => phrase.UserId == userId);
        if (existingPhrase is not null)
        {
            if (string.Equals(existingPhrase.Text, normalizedText, StringComparison.Ordinal))
            {
                return stateProjector.CreatePlayerSnapshot(game, userId);
            }

            throw new ApiException(
                "phrase_already_submitted",
                "A phrase has already been submitted for this round.",
                StatusCodes.Status409Conflict);
        }

        var phrase = new Phrase
        {
            RoundId = round.Id,
            Round = round,
            UserId = player.UserId,
            Text = normalizedText,
            SubmittedAt = clock.UtcNow
        };
        dbContext.Phrases.Add(phrase);

        var progress = GetSubmissionProgress(game, round, static (savedRound, participantId) =>
            savedRound.Phrases.Any(savedPhrase => savedPhrase.UserId == participantId));
        var phaseChanged = false;
        if (progress.Eligible > 0 && progress.Completed >= progress.Eligible)
        {
            AdvanceFromPhraseSubmission(round);
            phaseChanged = true;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        if (phaseChanged)
        {
            await PublishTransitionAsync(game, round);
        }

        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task<PlayerGameSnapshot> VotePhraseAsync(
        string gameCode,
        Guid userId,
        Guid phraseId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        EnsureMember(game, userId);
        var round = GetCurrentRound(game);
        EnsurePhase(round, RoundPhase.PhraseVoting);
        EnsureBeforeDeadline(round);

        var phrase = round.Phrases.SingleOrDefault(savedPhrase => savedPhrase.Id == phraseId)
            ?? throw new ApiException("phrase_not_found", "The phrase was not found in this round.", StatusCodes.Status404NotFound);
        if (phrase.UserId == userId)
        {
            throw new ApiException("self_vote_forbidden", "You cannot vote for your own phrase.", StatusCodes.Status409Conflict);
        }

        var existingVote = round.PhraseVotes.SingleOrDefault(vote => vote.UserId == userId);
        if (existingVote is not null)
        {
            if (existingVote.PhraseId == phraseId)
            {
                return stateProjector.CreatePlayerSnapshot(game, userId);
            }

            throw new ApiException(
                "phrase_vote_already_submitted",
                "A phrase vote has already been submitted for this round.",
                StatusCodes.Status409Conflict);
        }

        dbContext.PhraseVotes.Add(new()
        {
            RoundId = round.Id,
            Round = round,
            PhraseId = phrase.Id,
            Phrase = phrase,
            UserId = userId,
            CreatedAt = clock.UtcNow
        });

        var progress = GetSubmissionProgress(game, round, static (savedRound, participantId) =>
            savedRound.PhraseVotes.Any(vote => vote.UserId == participantId));
        var phaseChanged = false;
        if (progress.Eligible > 0 && progress.Completed >= progress.Eligible)
        {
            SelectWinningPhrase(round);
            phaseChanged = true;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        if (phaseChanged)
        {
            await PublishTransitionAsync(game, round);
        }

        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task<PlayerGameSnapshot> SubmitGifAsync(
        string gameCode,
        Guid userId,
        string selectionToken,
        CancellationToken cancellationToken)
    {
        var normalizedCode = gameCode.Trim().ToUpperInvariant();
        var selection = gifSelectionTokenService.Validate(selectionToken, normalizedCode);
        var gameId = await FindGameIdAsync(normalizedCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        EnsureMember(game, userId);
        var round = GetCurrentRound(game);
        EnsurePhase(round, RoundPhase.GifSubmission);
        EnsureBeforeDeadline(round);

        var submission = round.GifSubmissions.SingleOrDefault(saved => saved.UserId == userId);
        if (submission is null)
        {
            submission = new()
            {
                RoundId = round.Id,
                Round = round,
                UserId = userId
            };
            dbContext.GifSubmissions.Add(submission);
        }

        submission.Provider = selection.Provider;
        submission.ExternalId = selection.ExternalId;
        submission.Description = selection.Description;
        submission.PreviewUrl = selection.PreviewUrl;
        submission.MediaUrl = selection.MediaUrl;
        submission.Width = selection.Width;
        submission.Height = selection.Height;
        submission.PreviewWidth = selection.PreviewWidth;
        submission.PreviewHeight = selection.PreviewHeight;
        submission.SourceUrl = selection.SourceUrl;
        submission.Attribution = selection.Attribution;
        submission.SubmittedAt = clock.UtcNow;

        var progress = GetSubmissionProgress(game, round, static (savedRound, participantId) =>
            savedRound.GifSubmissions.Any(saved => saved.UserId == participantId));
        var phaseChanged = false;
        if (progress.Eligible > 0 && progress.Completed >= progress.Eligible)
        {
            AdvanceFromGifSubmission(game, round);
            phaseChanged = true;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        if (phaseChanged)
        {
            await PublishTransitionAsync(game, round);
        }

        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task<PlayerGameSnapshot> VoteGifAsync(
        string gameCode,
        Guid userId,
        Guid gifSubmissionId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        EnsureMember(game, userId);
        var round = GetCurrentRound(game);
        EnsurePhase(round, RoundPhase.GifVoting);
        EnsureBeforeDeadline(round);
        if (round.GifVotingPresentationEndsAt > clock.UtcNow)
        {
            throw new ApiException(
                "gif_presentation_in_progress",
                "GIF voting opens after every submission has been presented.",
                StatusCodes.Status409Conflict);
        }

        var submission = round.GifSubmissions.SingleOrDefault(saved => saved.Id == gifSubmissionId)
            ?? throw new ApiException("gif_not_found", "The GIF was not found in this round.", StatusCodes.Status404NotFound);
        if (submission.UserId == userId)
        {
            throw new ApiException("self_vote_forbidden", "You cannot vote for your own GIF.", StatusCodes.Status409Conflict);
        }

        var existingVote = round.GifVotes.SingleOrDefault(vote => vote.UserId == userId);
        if (existingVote is not null)
        {
            if (existingVote.GifSubmissionId == gifSubmissionId)
            {
                return stateProjector.CreatePlayerSnapshot(game, userId);
            }

            throw new ApiException(
                "gif_vote_already_submitted",
                "A GIF vote has already been submitted for this round.",
                StatusCodes.Status409Conflict);
        }

        dbContext.GifVotes.Add(new()
        {
            RoundId = round.Id,
            Round = round,
            GifSubmissionId = submission.Id,
            GifSubmission = submission,
            UserId = userId,
            CreatedAt = clock.UtcNow
        });

        var progress = GetGifVotingProgress(game, round);
        var phaseChanged = false;
        if (progress.Eligible > 0 && progress.Completed >= progress.Eligible)
        {
            CompleteGifVoting(game, round);
            phaseChanged = true;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        if (phaseChanged)
        {
            await PublishTransitionAsync(game, round);
        }

        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task<PlayerGameSnapshot> SetResultsReadyAsync(
        string gameCode,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        var player = EnsureMember(game, userId);
        var round = GetCurrentRound(game);
        EnsurePhase(round, RoundPhase.Results);
        EnsureBeforeDeadline(round);

        if (player.ResultReadyRoundNumber == round.RoundNumber)
        {
            return stateProjector.CreatePlayerSnapshot(game, userId);
        }

        player.ResultReadyRoundNumber = round.RoundNumber;
        var progress = GetResultsReadyProgress(game, round);
        var phaseChanged = progress.Eligible > 0 && progress.Completed >= progress.Eligible;
        var phaseToPublish = phaseChanged
            ? await CompleteResultsAsync(game, round, cancellationToken)
            : round;

        game.Version++;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        if (phaseChanged)
        {
            await PublishTransitionAsync(game, phaseToPublish);
        }

        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task ProcessExpiredRoundAsync(string gameCode, CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);

        var game = await LoadGameAsync(gameId, cancellationToken);
        var round = game.Rounds.SingleOrDefault(savedRound => savedRound.RoundNumber == game.CurrentRoundNumber);
        if (round is null || round.PhaseEndsAt > clock.UtcNow)
        {
            return;
        }

        await TryAdvanceExpiredRoundAsync(game, round, cancellationToken);
    }

    public async Task ProcessExpiredRoundsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expiredRoundIds = await dbContext.Rounds.AsNoTracking()
            .Where(round => round.PhaseEndsAt <= now &&
                (round.Phase == RoundPhase.PhraseSubmission ||
                 round.Phase == RoundPhase.PhraseVoting ||
                 round.Phase == RoundPhase.GifSubmission ||
                 round.Phase == RoundPhase.GifVoting ||
                 round.Phase == RoundPhase.Results))
            .OrderBy(round => round.PhaseEndsAt)
            .Select(round => round.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var roundId in expiredRoundIds)
        {
            await AdvanceExpiredRoundAsync(roundId, cancellationToken);
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task AdvanceExpiredRoundAsync(Guid roundId, CancellationToken cancellationToken)
    {
        var gameId = await dbContext.Rounds.AsNoTracking()
            .Where(round => round.Id == roundId)
            .Select(round => (Guid?)round.GameId)
            .SingleOrDefaultAsync(cancellationToken);
        if (gameId is null)
        {
            return;
        }

        await using var gameLock = await lockManager.AcquireAsync(gameId.Value, cancellationToken);
        var game = await LoadGameAsync(gameId.Value, cancellationToken);
        var round = game.Rounds.SingleOrDefault(savedRound => savedRound.Id == roundId);
        if (round is null || round.PhaseEndsAt > clock.UtcNow)
        {
            return;
        }

        await TryAdvanceExpiredRoundAsync(game, round, cancellationToken);
    }

    private async Task TryAdvanceExpiredRoundAsync(Game game, Round round, CancellationToken cancellationToken)
    {
        try
        {
            await AdvanceExpiredRoundAsync(game, round, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another scheduler instance or a realtime sync won the race.
            // The next snapshot reads the committed state, so this is safe to
            // treat as an idempotent no-op instead of blocking the scheduler.
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task AdvanceExpiredRoundAsync(Game game, Round round, CancellationToken cancellationToken)
    {
        Round phaseToPublish;
        switch (round.Phase)
        {
            case RoundPhase.PhraseSubmission:
                phaseToPublish = round.Phrases.Count == 0
                    ? await CompleteResultsAsync(game, round, cancellationToken)
                    : AdvanceFromPhraseSubmissionAndReturn(round);
                break;
            case RoundPhase.PhraseVoting:
                phaseToPublish = round.Phrases.Count == 0
                    ? await CompleteResultsAsync(game, round, cancellationToken)
                    : SelectWinningPhraseAndReturn(round);
                break;
            case RoundPhase.GifSubmission:
                AdvanceFromGifSubmission(game, round);
                phaseToPublish = round;
                break;
            case RoundPhase.GifVoting:
                CompleteGifVoting(game, round);
                phaseToPublish = round;
                break;
            case RoundPhase.Results:
                phaseToPublish = await CompleteResultsAsync(game, round, cancellationToken);
                break;
            default:
                return;
        }

        game.Version++;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishTransitionAsync(game, phaseToPublish);
    }

    private Round AdvanceFromPhraseSubmissionAndReturn(Round round)
    {
        AdvanceFromPhraseSubmission(round);
        return round;
    }

    private Round SelectWinningPhraseAndReturn(Round round)
    {
        SelectWinningPhrase(round);
        return round;
    }

    private async Task<Guid> FindGameIdAsync(string gameCode, CancellationToken cancellationToken)
    {
        var normalizedCode = gameCode.Trim().ToUpperInvariant();
        var gameId = await gameRepository.FindIdAsync(normalizedCode, cancellationToken);
        return gameId ?? throw new ApiException("game_not_found", "The game was not found.", StatusCodes.Status404NotFound);
    }

    private Task<Game> LoadGameAsync(Guid gameId, CancellationToken cancellationToken) =>
        gameRepository.LoadAsync(gameId, cancellationToken);

    private static GamePlayer EnsureMember(Game game, Guid userId) =>
        game.Players.SingleOrDefault(player => player.UserId == userId && player.LeftAt == null)
        ?? throw new ApiException("not_game_member", "You are not a member of this game.", StatusCodes.Status403Forbidden);

    private static Round GetCurrentRound(Game game) =>
        game.Rounds.SingleOrDefault(round => round.RoundNumber == game.CurrentRoundNumber)
        ?? throw new ApiException("round_not_found", "The current round was not found.", StatusCodes.Status409Conflict);

    private static void EnsurePhase(Round round, RoundPhase requiredPhase)
    {
        if (round.Phase != requiredPhase)
        {
            throw new ApiException(
                "invalid_round_phase",
                $"This command is only valid during {requiredPhase}.",
                StatusCodes.Status409Conflict);
        }
    }

    private void EnsureBeforeDeadline(Round round)
    {
        if (round.PhaseEndsAt <= clock.UtcNow)
        {
            throw new ApiException("phase_expired", "The round phase has already expired.", StatusCodes.Status409Conflict);
        }
    }
}
