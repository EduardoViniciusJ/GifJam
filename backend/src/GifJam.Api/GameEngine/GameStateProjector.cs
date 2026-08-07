using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;

namespace GifJam.Api.GameEngine;

public sealed class GameStateProjector(IClock clock, IRandomizer randomizer)
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
            game.TotalRounds,
            game.CurrentRoundNumber,
            game.HostUserId,
            canStart,
            players,
            clock.UtcNow);
    }

    public PresenceSnapshot CreatePresenceSnapshot(Game game) => new(
        game.Code,
        game.Players
            .OrderBy(player => player.JoinedAt)
            .Select(player => new PresencePlayerSnapshot(player.UserId, player.IsConnected))
            .ToArray(),
        clock.UtcNow);

    public RoundPhaseSnapshot CreatePhaseSnapshot(Round round)
    {
        var phrases = ShouldProjectPhrases(round.Phase)
            ? Shuffle(round.Phrases.Select(phrase => new AnonymousPhraseSnapshot(phrase.Id, phrase.Text)))
            : [];
        return new(
            round.RoundNumber,
            round.Phase,
            round.PhaseEndsAt,
            phrases,
            CreateSelectedPhrase(round),
            clock.UtcNow);
    }

    private PlayerRoundSnapshot CreatePlayerRoundSnapshot(Round round, Guid userId)
    {
        var phrases = ShouldProjectPhrases(round.Phase)
            ? Shuffle(round.Phrases.Select(phrase => new PlayerPhraseSnapshot(
                phrase.Id,
                phrase.Text,
                phrase.UserId == userId)))
            : [];
        return new(
            round.RoundNumber,
            round.Phase,
            round.PhaseEndsAt,
            round.Phrases.Any(phrase => phrase.UserId == userId),
            round.PhraseVotes.Any(vote => vote.UserId == userId),
            phrases,
            CreateSelectedPhrase(round),
            clock.UtcNow);
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

    private T[] Shuffle<T>(IEnumerable<T> source)
    {
        var items = source.ToList();
        randomizer.Shuffle(items);
        return [.. items];
    }

    private static bool ShouldProjectPhrases(RoundPhase phase) => phase == RoundPhase.PhraseVoting;
}
