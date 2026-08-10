using GifJam.Api.Common.Random;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.GameEngine;
using GifJam.Api.Tests.Auth;

namespace GifJam.Api.Tests.Games;

public sealed class GameDomainRuleTests
{
    [Fact]
    public void GameCodeContainsFiveUnambiguousCharacters()
    {
        var generator = new GameCodeGenerator(new SequentialRandomizer());

        var code = generator.Generate();

        Assert.Equal(5, code.Length);
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
    }

    [Fact]
    public void RankingUsesSharedPositionsForEqualScores()
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game { Code = "ABCDE", Status = GameStatus.Finished };
        game.Players.Add(CreatePlayer(game, "first", 5, now));
        game.Players.Add(CreatePlayer(game, "second", 5, now.AddSeconds(1)));
        game.Players.Add(CreatePlayer(game, "third", 2, now.AddSeconds(2)));
        var projector = new GameStateProjector(new TestClock(now), new SequentialRandomizer());

        var ranking = projector.CreateRankingSnapshot(game, isFinal: true);

        Assert.Equal([1, 1, 3], ranking.Entries.Select(entry => entry.Position));
        Assert.Equal([5, 5, 2], ranking.Entries.Select(entry => entry.Score));
    }

    private static GamePlayer CreatePlayer(Game game, string name, int score, DateTimeOffset joinedAt)
    {
        var user = new User
        {
            DiscordId = name,
            Username = name,
            DisplayName = name,
            CreatedAt = joinedAt,
            UpdatedAt = joinedAt
        };
        return new()
        {
            GameId = game.Id,
            Game = game,
            UserId = user.Id,
            User = user,
            Score = score,
            JoinedAt = joinedAt,
            LastSeenAt = joinedAt
        };
    }

    private sealed class SequentialRandomizer : IRandomizer
    {
        private int value;

        public int NextInt32(int exclusiveUpperBound)
        {
            var result = value % exclusiveUpperBound;
            value++;
            return result;
        }

        public void Shuffle<T>(IList<T> items)
        {
        }
    }
}
