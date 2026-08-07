using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Realtime;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.GameEngine;

public sealed class GameCoordinator(
    AppDbContext dbContext,
    IGameLockManager lockManager,
    IRandomizer randomizer,
    IClock clock,
    GameStateProjector stateProjector,
    IGameRealtimeNotifier realtimeNotifier,
    GifSelectionTokenService gifSelectionTokenService)
{
    private static readonly TimeSpan PhraseSubmissionDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PhraseVotingDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan GifSubmissionDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ResultsDuration = TimeSpan.FromSeconds(15);

    public async Task<PlayerGameSnapshot> StartGameAsync(
        string gameCode,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(gameCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);

        if (game.HostUserId != userId)
        {
            throw new ApiException("host_required", "Only the host can start the game.", StatusCodes.Status403Forbidden);
        }

        if (game.Status != GameStatus.Lobby)
        {
            throw new ApiException("game_already_started", "The game has already started.", StatusCodes.Status409Conflict);
        }

        if (game.Players.Count is < 2 or > 6 || game.Players.Any(player => player.UserId != userId && !player.IsReady))
        {
            throw new ApiException(
                "lobby_not_ready",
                "The game requires 2 to 6 players and every guest must be ready.",
                StatusCodes.Status409Conflict);
        }

        var now = clock.UtcNow;
        var round = new Round
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = 1,
            Phase = RoundPhase.PhraseSubmission,
            PhaseEndsAt = now.Add(PhraseSubmissionDuration),
            StartedAt = now
        };
        game.Status = GameStatus.InProgress;
        game.StartedAt = now;
        game.CurrentRoundNumber = 1;
        game.Version++;
        dbContext.Rounds.Add(round);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishPhaseAsync(game, round);
        await realtimeNotifier.LobbyUpdatedAsync(
            game.Code,
            stateProjector.CreateLobbySnapshot(game),
            CancellationToken.None);
        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

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
            await PublishPhaseAsync(game, round);
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
            await PublishPhaseAsync(game, round);
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
        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await realtimeNotifier.SubmissionProgressAsync(game.Code, progress, CancellationToken.None);
        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task ProcessExpiredRoundsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var expiredRoundIds = await dbContext.Rounds.AsNoTracking()
            .Where(round => round.PhaseEndsAt <= now &&
                (round.Phase == RoundPhase.PhraseSubmission ||
                 round.Phase == RoundPhase.PhraseVoting ||
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var game = await LoadGameAsync(gameId.Value, cancellationToken);
        var round = game.Rounds.SingleOrDefault(savedRound => savedRound.Id == roundId);
        if (round is null || round.PhaseEndsAt > clock.UtcNow)
        {
            return;
        }

        Round phaseToPublish;
        switch (round.Phase)
        {
            case RoundPhase.PhraseSubmission:
                AdvanceFromPhraseSubmission(round);
                phaseToPublish = round;
                break;
            case RoundPhase.PhraseVoting:
                SelectWinningPhrase(round);
                phaseToPublish = round;
                break;
            case RoundPhase.Results:
                phaseToPublish = CompleteResults(game, round);
                break;
            default:
                return;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await PublishPhaseAsync(game, phaseToPublish);
    }

    private void AdvanceFromPhraseSubmission(Round round)
    {
        switch (round.Phrases.Count)
        {
            case 0:
                round.Phase = RoundPhase.Results;
                round.PhaseEndsAt = clock.UtcNow.Add(ResultsDuration);
                break;
            case 1:
                var phrase = round.Phrases.Single();
                round.SelectedPhraseId = phrase.Id;
                round.SelectedPhrase = phrase;
                round.Phase = RoundPhase.GifSubmission;
                round.PhaseEndsAt = clock.UtcNow.Add(GifSubmissionDuration);
                break;
            default:
                round.Phase = RoundPhase.PhraseVoting;
                round.PhaseEndsAt = clock.UtcNow.Add(PhraseVotingDuration);
                break;
        }
    }

    private void SelectWinningPhrase(Round round)
    {
        if (round.Phrases.Count == 0)
        {
            round.Phase = RoundPhase.Results;
            round.PhaseEndsAt = clock.UtcNow.Add(ResultsDuration);
            return;
        }

        var voteCounts = round.Phrases.ToDictionary(phrase => phrase.Id, _ => 0);
        foreach (var vote in round.PhraseVotes)
        {
            voteCounts[vote.PhraseId]++;
        }

        var highestVoteCount = voteCounts.Values.Max();
        var leaders = round.Phrases.Where(phrase => voteCounts[phrase.Id] == highestVoteCount).ToArray();
        var selected = leaders[randomizer.NextInt32(leaders.Length)];
        round.SelectedPhraseId = selected.Id;
        round.SelectedPhrase = selected;
        round.Phase = RoundPhase.GifSubmission;
        round.PhaseEndsAt = clock.UtcNow.Add(GifSubmissionDuration);
    }

    private Round CompleteResults(Game game, Round round)
    {
        round.Phase = RoundPhase.Completed;
        round.FinishedAt = clock.UtcNow;
        if (game.CurrentRoundNumber >= game.TotalRounds)
        {
            game.Status = GameStatus.Finished;
            game.FinishedAt = clock.UtcNow;
            return round;
        }

        game.CurrentRoundNumber++;
        var nextRound = new Round
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = game.CurrentRoundNumber,
            Phase = RoundPhase.PhraseSubmission,
            PhaseEndsAt = clock.UtcNow.Add(PhraseSubmissionDuration),
            StartedAt = clock.UtcNow
        };
        dbContext.Rounds.Add(nextRound);
        return nextRound;
    }

    private SubmissionProgressSnapshot GetSubmissionProgress(
        Game game,
        Round round,
        Func<Round, Guid, bool> hasCompleted)
    {
        var eligiblePlayers = game.Players.Where(player => player.IsConnected).Select(player => player.UserId).ToArray();
        var completed = eligiblePlayers.Count(playerId => hasCompleted(round, playerId));
        return new(completed, eligiblePlayers.Length, clock.UtcNow);
    }

    private async Task PublishPhaseAsync(Game game, Round round) =>
        await realtimeNotifier.PhaseChangedAsync(
            game.Code,
            stateProjector.CreatePhaseSnapshot(round),
            CancellationToken.None);

    private async Task<Guid> FindGameIdAsync(string gameCode, CancellationToken cancellationToken)
    {
        var normalizedCode = gameCode.Trim().ToUpperInvariant();
        var gameId = await dbContext.Games.AsNoTracking()
            .Where(game => game.Code == normalizedCode && game.Status != GameStatus.Closed)
            .Select(game => (Guid?)game.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return gameId ?? throw new ApiException("game_not_found", "The game was not found.", StatusCodes.Status404NotFound);
    }

    private async Task<Game> LoadGameAsync(Guid gameId, CancellationToken cancellationToken) =>
        await dbContext.Games
            .Include(game => game.Players)
            .ThenInclude(player => player.User)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.Phrases)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.PhraseVotes)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.GifSubmissions)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.GifVotes)
            .Where(game => game.Id == gameId)
            .AsSplitQuery()
            .SingleAsync(cancellationToken);

    private static GamePlayer EnsureMember(Game game, Guid userId) =>
        game.Players.SingleOrDefault(player => player.UserId == userId)
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
