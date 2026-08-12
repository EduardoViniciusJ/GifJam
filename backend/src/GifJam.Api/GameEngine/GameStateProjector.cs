using GifJam.Api.Common.Time;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using System.Security.Cryptography;

namespace GifJam.Api.GameEngine;

public sealed class GameStateProjector(IClock clock)
{
    public PlayerGameSnapshot CreatePlayerSnapshot(Game game, Guid userId)
    {
        var lobby = CreateLobbySnapshot(game);
        var round = game.Rounds.SingleOrDefault(savedRound => savedRound.RoundNumber == game.CurrentRoundNumber);
        return new(lobby, game.HostUserId == userId, round is null ? null : CreatePlayerRoundSnapshot(round, userId));
    }

    public LobbySnapshot CreateLobbySnapshot(Game game)
    {
        var players = game.Players
            .Where(player => player.LeftAt is null)
            .OrderBy(player => player.JoinedAt)
            .Select(player => new LobbyPlayerSnapshot(
                player.UserId,
                player.User.Username,
                player.User.DisplayName,
                player.User.AvatarUrl,
                player.Score,
                player.IsReady,
                player.IsConnected,
                player.UserId == game.HostUserId))
            .ToArray();
        var canStart = players.Length >= 2 && players.Where(player => !player.IsHost).All(player => player.IsReady);

        return new(
            game.Code,
            game.Status,
            game.Mode,
            game.TotalRounds,
            game.PhraseSubmissionSeconds,
            game.ResultsSeconds,
            game.CurrentRoundNumber,
            game.HostUserId,
            canStart,
            players,
            clock.UtcNow);
    }

    public PresenceSnapshot CreatePresenceSnapshot(Game game) => new(
        game.Code,
        game.Players
            .Where(player => player.LeftAt is null)
            .OrderBy(player => player.JoinedAt)
            .Select(player => new PresencePlayerSnapshot(player.UserId, player.IsConnected))
            .ToArray(),
        clock.UtcNow);

    public RoundPhaseSnapshot CreatePhaseSnapshot(Round round)
    {
        var phrases = ShouldProjectPhrases(round.Phase)
            ? OrderForRound(
                round.Id,
                round.Phrases.Select(phrase => new AnonymousPhraseSnapshot(phrase.Id, phrase.Text)),
                phrase => phrase.Id)
            : [];
        var gifs = ShouldProjectGifs(round.Phase)
            ? OrderForRound(
                round.Id,
                round.GifSubmissions.Select(CreateAnonymousGifSnapshot),
                gif => gif.Id)
            : [];
        return new(
            round.RoundNumber,
            round.Phase,
            round.PhaseEndsAt,
            round.Phase == RoundPhase.GifVoting ? round.GifVotingPresentationEndsAt : null,
            phrases,
            gifs,
            CreateSelectedPhrase(round),
            clock.UtcNow);
    }

    private PlayerRoundSnapshot CreatePlayerRoundSnapshot(Round round, Guid userId)
    {
        var phrases = ShouldProjectPhrases(round.Phase)
            ? OrderForRound(
                round.Id,
                round.Phrases.Select(phrase => new PlayerPhraseSnapshot(
                    phrase.Id,
                    phrase.Text,
                    phrase.UserId == userId)),
                phrase => phrase.Id)
            : [];
        var gifs = ShouldProjectGifs(round.Phase)
            ? OrderForRound(
                round.Id,
                round.GifSubmissions.Select(submission => new PlayerGifSnapshot(
                    submission.Id,
                    submission.Description,
                    submission.PreviewUrl,
                    submission.MediaUrl,
                    submission.Width,
                    submission.Height,
                    submission.PreviewWidth,
                    submission.PreviewHeight,
                    submission.SourceUrl,
                    submission.Attribution,
                    submission.UserId == userId)),
                gif => gif.Id)
            : [];
        var gifSelection = round.GifSubmissions.SingleOrDefault(submission => submission.UserId == userId);
        var game = round.Game;
        var player = game.Players.Single(savedPlayer =>
            savedPlayer.UserId == userId && savedPlayer.LeftAt == null);
        var reveal = round.Phase is RoundPhase.Results or RoundPhase.Completed
            ? CreateRoundRevealSnapshot(round)
            : null;
        var ranking = round.Phase is RoundPhase.Results or RoundPhase.Completed
            ? CreateRankingSnapshot(game, game.Status == GameStatus.Finished)
            : null;
        return new(
            round.RoundNumber,
            round.Phase,
            round.PhaseEndsAt,
            round.Phase == RoundPhase.GifVoting ? round.GifVotingPresentationEndsAt : null,
            round.Phrases.Any(phrase => phrase.UserId == userId),
            round.PhraseVotes.Any(vote => vote.UserId == userId),
            gifSelection is not null,
            round.GifVotes.Any(vote => vote.UserId == userId),
            player.ResultReadyRoundNumber == round.RoundNumber,
            phrases,
            gifs,
            CreateSelectedPhrase(round),
            gifSelection is null ? null : new(
                gifSelection.ExternalId,
                gifSelection.Description,
                gifSelection.PreviewUrl,
                gifSelection.MediaUrl,
                gifSelection.Width,
                gifSelection.Height,
                gifSelection.PreviewWidth,
                gifSelection.PreviewHeight,
                gifSelection.SourceUrl,
                gifSelection.Attribution),
            reveal,
            ranking,
            clock.UtcNow);
    }

    public RoundRevealSnapshot CreateRoundRevealSnapshot(Round round)
    {
        var voteCounts = round.GifSubmissions.ToDictionary(
            submission => submission.Id,
            submission => submission.Votes.Count);
        var positions = CreateSharedPositions(voteCounts);
        var gifs = round.GifSubmissions
            .OrderBy(submission => positions[submission.Id])
            .ThenBy(submission => submission.SubmittedAt)
            .Select(submission => new RevealedGifSnapshot(
                submission.Id,
                submission.Description,
                submission.PreviewUrl,
                submission.MediaUrl,
                submission.Width,
                submission.Height,
                submission.PreviewWidth,
                submission.PreviewHeight,
                submission.SourceUrl,
                submission.Attribution,
                CreateRevealedPlayer(submission.User),
                voteCounts[submission.Id],
                positions[submission.Id]))
            .ToArray();
        RevealedPhraseSnapshot? phrase = null;
        if (round.SelectedPhrase is not null)
        {
            phrase = new(
                round.SelectedPhrase.Id,
                round.SelectedPhrase.Text,
                round.SelectedPhrase.Source,
                round.SelectedPhrase.User is null
                    ? null
                    : CreateRevealedPlayer(round.SelectedPhrase.User));
        }

        return new(round.RoundNumber, phrase, gifs, clock.UtcNow);
    }

    public RankingSnapshot CreateRankingSnapshot(Game game, bool isFinal)
    {
        var orderedPlayers = game.Players
            .Where(player => player.LeftAt is null)
            .OrderByDescending(player => player.Score)
            .ThenBy(player => player.JoinedAt)
            .ToArray();
        var entries = new List<RankingEntrySnapshot>(orderedPlayers.Length);
        for (var index = 0; index < orderedPlayers.Length; index++)
        {
            var player = orderedPlayers[index];
            var position = index > 0 && orderedPlayers[index - 1].Score == player.Score
                ? entries[index - 1].Position
                : index + 1;
            entries.Add(new(
                position,
                player.UserId,
                player.User.Username,
                player.User.DisplayName,
                player.User.AvatarUrl,
                player.Score));
        }

        var currentRound = game.Rounds.SingleOrDefault(round => round.RoundNumber == game.CurrentRoundNumber);
        var completedRounds = game.Status == GameStatus.Finished
            ? game.TotalRounds
            : Math.Max(0, game.CurrentRoundNumber -
                (currentRound?.Phase is RoundPhase.Results or RoundPhase.Completed ? 0 : 1));
        return new(game.Code, completedRounds, isFinal, entries, clock.UtcNow);
    }

    private static SelectedPhraseSnapshot? CreateSelectedPhrase(Round round)
    {
        if (round.SelectedPhraseId is null)
        {
            return null;
        }

        var phrase = round.Phrases.Single(savedPhrase => savedPhrase.Id == round.SelectedPhraseId);
        return new(phrase.Id, phrase.Text);
    }

    private static AnonymousGifSnapshot CreateAnonymousGifSnapshot(GifSubmission submission) => new(
        submission.Id,
        submission.Description,
        submission.PreviewUrl,
        submission.MediaUrl,
        submission.Width,
        submission.Height,
        submission.PreviewWidth,
        submission.PreviewHeight,
        submission.SourceUrl,
        submission.Attribution);

    private static RevealedPlayerSnapshot CreateRevealedPlayer(User user) => new(
        user.Id,
        user.Username,
        user.DisplayName,
        user.AvatarUrl);

    private static Dictionary<Guid, int> CreateSharedPositions(IReadOnlyDictionary<Guid, int> voteCounts)
    {
        var ordered = voteCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).ToArray();
        var positions = new Dictionary<Guid, int>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var position = index > 0 && ordered[index - 1].Value == ordered[index].Value
                ? positions[ordered[index - 1].Key]
                : index + 1;
            positions[ordered[index].Key] = position;
        }

        return positions;
    }

    private static T[] OrderForRound<T>(Guid roundId, IEnumerable<T> source, Func<T, Guid> idSelector) =>
        [.. source.OrderBy(item => CreateOrderKey(roundId, idSelector(item)), StringComparer.Ordinal)];

    private static string CreateOrderKey(Guid roundId, Guid itemId)
    {
        Span<byte> input = stackalloc byte[32];
        roundId.TryWriteBytes(input[..16]);
        itemId.TryWriteBytes(input[16..]);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static bool ShouldProjectPhrases(RoundPhase phase) => phase == RoundPhase.PhraseVoting;

    private static bool ShouldProjectGifs(RoundPhase phase) => phase == RoundPhase.GifVoting;
}
