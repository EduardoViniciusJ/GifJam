using System.Text.Json;
using GifJam.Api.Common.Errors;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Features.Games;
using GifJam.Api.GameEngine;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GifVotingAndRankingTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;

    public GifVotingAndRankingTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
    }

    [Fact]
    public async Task GifVotingIsAnonymousRejectsOwnVoteAndRevealsAuthorsWithSharedRank()
    {
        var setup = await CreateReadyGameAsync();
        await StartRoundAndSelectPhraseAsync(setup, 1);
        await SubmitBothGifsAsync(setup, 1);

        var gifIds = await LoadGifIdsAsync(1);
        await using (var context = database.CreateDbContext())
        {
            var round = await context.Rounds
                .Include(savedRound => savedRound.GifSubmissions)
                .Include(savedRound => savedRound.Phrases)
                .SingleAsync();
            using var scope = factory.Services.CreateScope();
            var publicSnapshot = scope.ServiceProvider.GetRequiredService<GameStateProjector>()
                .CreatePhaseSnapshot(round);
            var json = JsonSerializer.Serialize(publicSnapshot, JsonOptions);
            Assert.Equal(RoundPhase.GifVoting, publicSnapshot.Phase);
            Assert.Equal(2, publicSnapshot.Gifs.Count);
            Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("displayName", json, StringComparison.OrdinalIgnoreCase);
        }

        var hostSnapshot = await WithGameServiceAsync(service => service.GetAsync(
            setup.Code,
            setup.Host.Id,
            CancellationToken.None));
        Assert.Single(hostSnapshot.Round!.Gifs, gif => gif.IsOwn);

        var selfVote = await Assert.ThrowsAsync<ApiException>(() => WithCoordinatorAsync(coordinator =>
            coordinator.VoteGifAsync(setup.Code, setup.Host.Id, gifIds[setup.Host.Id], CancellationToken.None)));
        Assert.Equal("self_vote_forbidden", selfVote.Code);

        await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
            setup.Code,
            setup.Host.Id,
            gifIds[setup.Guest.Id],
            CancellationToken.None));
        await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
            setup.Code,
            setup.Host.Id,
            gifIds[setup.Guest.Id],
            CancellationToken.None));
        var result = await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
            setup.Code,
            setup.Guest.Id,
            gifIds[setup.Host.Id],
            CancellationToken.None));

        Assert.Equal(RoundPhase.Results, result.Round?.Phase);
        Assert.NotNull(result.Round?.Reveal?.Phrase?.Author);
        Assert.Equal(2, result.Round?.Reveal?.Gifs.Count);
        Assert.All(result.Round!.Reveal!.Gifs, gif => Assert.Equal(1, gif.VoteCount));
        Assert.All(result.Round.Ranking!.Entries, entry =>
        {
            Assert.Equal(1, entry.Position);
            Assert.Equal(1, entry.Score);
        });
        await using var savedContext = database.CreateDbContext();
        Assert.Equal(2, await savedContext.GifVotes.CountAsync());
    }

    [Fact]
    public async Task ZeroOrOneGifSubmissionMovesDirectlyToResults()
    {
        var noGifSetup = await CreateReadyGameAsync();
        await StartRoundAndSelectPhraseAsync(noGifSetup, 1);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(61);
        await ProcessExpiredAsync();
        await using (var context = database.CreateDbContext())
        {
            Assert.Equal(RoundPhase.Results, (await context.Rounds.SingleAsync()).Phase);
        }

        var oneGifSetup = await CreateReadyGameAsync();
        await StartRoundAndSelectPhraseAsync(oneGifSetup, 1);
        await SubmitGifAsync(oneGifSetup.Code, oneGifSetup.Host, 1);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(61);
        await ProcessExpiredAsync();
        await using var savedContext = database.CreateDbContext();
        var savedRound = await savedContext.Rounds.SingleAsync();
        Assert.Equal(RoundPhase.Results, savedRound.Phase);
        Assert.All(await savedContext.GamePlayers.ToArrayAsync(), player => Assert.Equal(0, player.Score));
    }

    [Fact]
    public async Task ThreeRoundTwoPlayerGameFinishesWithExpectedTie()
    {
        var setup = await CreateReadyGameAsync();
        await WithCoordinatorAsync(coordinator => coordinator.StartGameAsync(
            setup.Code,
            setup.Host.Id,
            CancellationToken.None));

        for (var roundNumber = 1; roundNumber <= 3; roundNumber++)
        {
            await PlayRoundAsync(setup, roundNumber);
            factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(16);
            await ProcessExpiredAsync();
        }

        await using var context = database.CreateDbContext();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .ThenInclude(player => player.User)
            .Include(savedGame => savedGame.Rounds)
            .SingleAsync();
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(3, game.Rounds.Count);
        Assert.All(game.Rounds, round => Assert.Equal(RoundPhase.Completed, round.Phase));
        Assert.All(game.Players, player => Assert.Equal(3, player.Score));

        using var scope = factory.Services.CreateScope();
        var ranking = scope.ServiceProvider.GetRequiredService<GameStateProjector>()
            .CreateRankingSnapshot(game, isFinal: true);
        Assert.True(ranking.IsFinal);
        Assert.All(ranking.Entries, entry => Assert.Equal(1, entry.Position));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public async Task ThreeRoundGameFinishesForHomologationPlayerCounts(int playerCount)
    {
        var setup = await CreateReadyGameAsync(playerCount);
        await WithCoordinatorAsync(coordinator => coordinator.StartGameAsync(
            setup.Code,
            setup.Players[0].Id,
            CancellationToken.None));

        for (var roundNumber = 1; roundNumber <= 3; roundNumber++)
        {
            await PlayHomologationRoundAsync(setup, roundNumber);
            factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(16);
            await ProcessExpiredAsync();
        }

        await using var context = database.CreateDbContext();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .Include(savedGame => savedGame.Rounds)
            .SingleAsync();
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Equal(3, game.Rounds.Count);
        Assert.All(game.Rounds, round => Assert.Equal(RoundPhase.Completed, round.Phase));
        Assert.All(game.Players, player => Assert.Equal(3, player.Score));
    }

    [Fact]
    public async Task DuplicateCommandsAndSimultaneousTimeoutRemainIdempotent()
    {
        var setup = await CreateReadyGameAsync();
        await StartRoundAndSelectPhraseAsync(setup, 1);
        string hostToken;
        using (var scope = factory.Services.CreateScope())
        {
            hostToken = scope.ServiceProvider.GetRequiredService<GifSelectionTokenService>()
                .Create(setup.Code, CreateGifItem(setup.Host.Username, 1));
        }

        await Task.WhenAll(
            WithCoordinatorAsync(coordinator => coordinator.SubmitGifAsync(
                setup.Code,
                setup.Host.Id,
                hostToken,
                CancellationToken.None)),
            WithCoordinatorAsync(coordinator => coordinator.SubmitGifAsync(
                setup.Code,
                setup.Host.Id,
                hostToken,
                CancellationToken.None)));
        await using (var submissionContext = database.CreateDbContext())
        {
            Assert.Equal(1, await submissionContext.GifSubmissions.CountAsync());
            Assert.Equal(RoundPhase.GifSubmission, (await submissionContext.Rounds.SingleAsync()).Phase);
        }

        await SubmitGifAsync(setup.Code, setup.Guest, 1);
        var gifIds = await LoadGifIdsAsync(1);
        await Task.WhenAll(
            WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
                setup.Code,
                setup.Host.Id,
                gifIds[setup.Guest.Id],
                CancellationToken.None)),
            WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
                setup.Code,
                setup.Host.Id,
                gifIds[setup.Guest.Id],
                CancellationToken.None)));

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(21);
        var lateVote = WithCoordinatorAsync(async coordinator =>
        {
            try
            {
                await coordinator.VoteGifAsync(
                    setup.Code,
                    setup.Guest.Id,
                    gifIds[setup.Host.Id],
                    CancellationToken.None);
            }
            catch (ApiException exception) when (exception.Code is "phase_expired" or "invalid_round_phase")
            {
            }

            return true;
        });
        await Task.WhenAll(lateVote, ProcessExpiredAsync());

        await using var context = database.CreateDbContext();
        Assert.Equal(RoundPhase.Results, (await context.Rounds.SingleAsync()).Phase);
        Assert.Equal(1, await context.GifVotes.CountAsync());
        Assert.Equal(1, await context.GamePlayers.SumAsync(player => player.Score));
    }

    public void Dispose()
    {
        factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task PlayHomologationRoundAsync(PlayerGameSetup setup, int roundNumber)
    {
        foreach (var player in setup.Players)
        {
            await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
                setup.Code,
                player.Id,
                $"{player.Username} phrase {roundNumber}",
                CancellationToken.None));
        }

        await using (var context = database.CreateDbContext())
        {
            var phraseIds = await context.Phrases
                .Where(phrase => phrase.Round.RoundNumber == roundNumber)
                .ToDictionaryAsync(phrase => phrase.UserId, phrase => phrase.Id);
            for (var index = 0; index < setup.Players.Count; index++)
            {
                var voter = setup.Players[index];
                var target = setup.Players[(index + 1) % setup.Players.Count];
                await WithCoordinatorAsync(coordinator => coordinator.VotePhraseAsync(
                    setup.Code,
                    voter.Id,
                    phraseIds[target.Id],
                    CancellationToken.None));
            }
        }

        foreach (var player in setup.Players)
        {
            await SubmitGifAsync(setup.Code, player, roundNumber);
        }

        var gifIds = await LoadGifIdsAsync(roundNumber);
        for (var index = 0; index < setup.Players.Count; index++)
        {
            var voter = setup.Players[index];
            var target = setup.Players[(index + 1) % setup.Players.Count];
            await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
                setup.Code,
                voter.Id,
                gifIds[target.Id],
                CancellationToken.None));
        }
    }

    private async Task PlayRoundAsync(GameSetup setup, int roundNumber)
    {
        await SubmitPhrasesAndVotesAsync(setup, roundNumber);
        await SubmitBothGifsAsync(setup, roundNumber);
        var gifIds = await LoadGifIdsAsync(roundNumber);
        await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
            setup.Code,
            setup.Host.Id,
            gifIds[setup.Guest.Id],
            CancellationToken.None));
        var result = await WithCoordinatorAsync(coordinator => coordinator.VoteGifAsync(
            setup.Code,
            setup.Guest.Id,
            gifIds[setup.Host.Id],
            CancellationToken.None));
        Assert.Equal(RoundPhase.Results, result.Round?.Phase);
    }

    private async Task StartRoundAndSelectPhraseAsync(GameSetup setup, int roundNumber)
    {
        await WithCoordinatorAsync(coordinator => coordinator.StartGameAsync(
            setup.Code,
            setup.Host.Id,
            CancellationToken.None));
        await SubmitPhrasesAndVotesAsync(setup, roundNumber);
    }

    private async Task SubmitPhrasesAndVotesAsync(GameSetup setup, int roundNumber)
    {
        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Host.Id,
            $"Host phrase {roundNumber}",
            CancellationToken.None));
        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Guest.Id,
            $"Guest phrase {roundNumber}",
            CancellationToken.None));
        await using var context = database.CreateDbContext();
        var phraseIds = await context.Phrases
            .Where(phrase => phrase.Round.RoundNumber == roundNumber)
            .ToDictionaryAsync(phrase => phrase.UserId, phrase => phrase.Id);
        await WithCoordinatorAsync(coordinator => coordinator.VotePhraseAsync(
            setup.Code,
            setup.Host.Id,
            phraseIds[setup.Guest.Id],
            CancellationToken.None));
        await WithCoordinatorAsync(coordinator => coordinator.VotePhraseAsync(
            setup.Code,
            setup.Guest.Id,
            phraseIds[setup.Host.Id],
            CancellationToken.None));
    }

    private async Task SubmitBothGifsAsync(GameSetup setup, int roundNumber)
    {
        await SubmitGifAsync(setup.Code, setup.Host, roundNumber);
        await SubmitGifAsync(setup.Code, setup.Guest, roundNumber);
    }

    private async Task SubmitGifAsync(string gameCode, User user, int roundNumber)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<GifSelectionTokenService>().Create(
            gameCode,
            CreateGifItem(user.Username, roundNumber));
        await WithCoordinatorAsync(coordinator => coordinator.SubmitGifAsync(
            gameCode,
            user.Id,
            token,
            CancellationToken.None));
    }

    private async Task<Dictionary<Guid, Guid>> LoadGifIdsAsync(int roundNumber)
    {
        await using var context = database.CreateDbContext();
        return await context.GifSubmissions
            .Where(submission => submission.Round.RoundNumber == roundNumber)
            .ToDictionaryAsync(submission => submission.UserId, submission => submission.Id);
    }

    private async Task ProcessExpiredAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameCoordinator>()
            .ProcessExpiredRoundsAsync(CancellationToken.None);
    }

    private async Task<GameSetup> CreateReadyGameAsync()
    {
        await database.ResetAsync();
        User[] users;
        await using (var context = database.CreateDbContext())
        {
            users = [CreateUser("ranking-host"), CreateUser("ranking-guest")];
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        var created = await WithGameServiceAsync(service => service.CreateAsync(users[0].Id, 3, CancellationToken.None));
        await WithGameServiceAsync(service => service.JoinAsync(created.Lobby.Code, users[1].Id, CancellationToken.None));
        await WithGameServiceAsync(service => service.SetReadyAsync(created.Lobby.Code, users[1].Id, true, CancellationToken.None));
        return new(created.Lobby.Code, users[0], users[1]);
    }

    private async Task<PlayerGameSetup> CreateReadyGameAsync(int playerCount)
    {
        await database.ResetAsync();
        var users = Enumerable.Range(1, playerCount)
            .Select(index => CreateUser($"homologation-{playerCount}-{index}"))
            .ToArray();
        await using (var context = database.CreateDbContext())
        {
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        var created = await WithGameServiceAsync(service => service.CreateAsync(users[0].Id, 3, CancellationToken.None));
        foreach (var guest in users.Skip(1))
        {
            await WithGameServiceAsync(service => service.JoinAsync(created.Lobby.Code, guest.Id, CancellationToken.None));
            await WithGameServiceAsync(service => service.SetReadyAsync(
                created.Lobby.Code,
                guest.Id,
                true,
                CancellationToken.None));
        }

        return new(created.Lobby.Code, users);
    }

    private async Task<T> WithCoordinatorAsync<T>(Func<GameCoordinator, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<GameCoordinator>());
    }

    private async Task<T> WithGameServiceAsync<T>(Func<GameService, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<GameService>());
    }

    private User CreateUser(string discordId) => new()
    {
        DiscordId = discordId,
        Username = discordId,
        DisplayName = discordId,
        CreatedAt = factory.Clock.UtcNow,
        UpdatedAt = factory.Clock.UtcNow
    };

    private static GifProviderItem CreateGifItem(string name, int roundNumber) => new(
        $"{name}-{roundNumber}",
        $"{name} reaction",
        $"https://static.klipy.test/{name}-{roundNumber}-preview.gif",
        $"https://static.klipy.test/{name}-{roundNumber}.gif",
        480,
        270,
        240,
        135,
        $"https://klipy.test/gifs/{name}-{roundNumber}",
        "Powered by KLIPY");

    private sealed record GameSetup(string Code, User Host, User Guest);

    private sealed record PlayerGameSetup(string Code, IReadOnlyList<User> Players);
}
