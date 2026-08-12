using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Domain.Rules;
using GifJam.Api.Features.AiPhrases;

namespace GifJam.Api.Features.Games.Services;

public sealed partial class GameRoundService
{
    private void AdvanceFromPhraseSubmission(Round round)
    {
        switch (round.Phrases.Count)
        {
            case 0:
                round.Phase = RoundPhase.Results;
                round.PhaseEndsAt = clock.UtcNow.AddSeconds(round.Game.ResultsSeconds);
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
            round.PhaseEndsAt = clock.UtcNow.AddSeconds(round.Game.ResultsSeconds);
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

    private void AdvanceFromGifSubmission(Game game, Round round)
    {
        if (round.GifSubmissions.Count < GameRules.MinimumPlayers)
        {
            CompleteGifVoting(game, round);
            return;
        }

        round.Phase = RoundPhase.GifVoting;
        round.GifVotingPresentationEndsAt = clock.UtcNow.Add(
            TimeSpan.FromTicks(GifPresentationDurationPerItem.Ticks * round.GifSubmissions.Count));
        round.PhaseEndsAt = round.GifVotingPresentationEndsAt.Value.Add(GifVotingSelectionDuration);
    }

    private void CompleteGifVoting(Game game, Round round)
    {
        var voteCounts = round.GifVotes
            .GroupBy(vote => vote.GifSubmissionId)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var submission in round.GifSubmissions)
        {
            if (voteCounts.TryGetValue(submission.Id, out var receivedVotes))
            {
                var player = game.Players.Single(player => player.UserId == submission.UserId);
                player.Score += receivedVotes;
                player.User.TotalScore += receivedVotes;
            }
        }

        round.Phase = RoundPhase.Results;
        round.PhaseEndsAt = clock.UtcNow.AddSeconds(game.ResultsSeconds);
    }

    private async Task<Round> CompleteResultsAsync(
        Game game,
        Round round,
        CancellationToken cancellationToken)
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
        return await CreateRoundAsync(game, game.CurrentRoundNumber, cancellationToken);
    }

    private async Task<Round> CreateRoundAsync(
        Game game,
        int roundNumber,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var isAiMode = game.Mode == GameMode.AiRandomPhrases;
        var round = new Round
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = roundNumber,
            Phase = isAiMode ? RoundPhase.PhraseVoting : RoundPhase.PhraseSubmission,
            PhaseEndsAt = isAiMode
                ? now.Add(PhraseVotingDuration)
                : now.AddSeconds(game.PhraseSubmissionSeconds),
            StartedAt = now
        };

        if (isAiMode)
        {
            var playerNames = game.Players
                .Where(player => player.LeftAt is null)
                .OrderBy(player => player.JoinedAt)
                .Select(player => player.User.DisplayName)
                .ToArray();
            var phrases = await aiPhraseGenerationService.GenerateAsync(
                playerNames,
                roundNumber,
                cancellationToken);
            foreach (var text in phrases)
            {
                round.Phrases.Add(new()
                {
                    RoundId = round.Id,
                    Round = round,
                    Source = PhraseSource.Ai,
                    Text = text,
                    SubmittedAt = now
                });
            }
        }

        dbContext.Rounds.Add(round);
        return round;
    }
}
