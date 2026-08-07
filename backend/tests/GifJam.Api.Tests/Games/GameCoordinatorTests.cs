using System.Text.Json;
using GifJam.Api.Common.Errors;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.GameEngine;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GameCoordinatorTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;

    public GameCoordinatorTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
    }

    [Fact]
    public async Task HostStartsReadyLobbyWithThirtySecondPhraseDeadline()
    {
        var setup = await CreateReadyGameAsync();

        var snapshot = await WithCoordinatorAsync(coordinator => coordinator.StartGameAsync(
            setup.Code,
            setup.Host.Id,
            CancellationToken.None));

        Assert.NotNull(snapshot.Round);
        Assert.Equal(RoundPhase.PhraseSubmission, snapshot.Round.Phase);
        Assert.Equal(factory.Clock.UtcNow.AddSeconds(30), snapshot.Round.PhaseEndsAt);
        await using var context = database.CreateDbContext();
        var game = await context.Games.Include(savedGame => savedGame.Rounds).SingleAsync();
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(1, game.CurrentRoundNumber);
        Assert.Equal(3, game.Version);
    }

    [Fact]
    public async Task GuestCannotStartGame()
    {
        var setup = await CreateReadyGameAsync();

        var exception = await Assert.ThrowsAsync<ApiException>(() => WithCoordinatorAsync(coordinator =>
            coordinator.StartGameAsync(setup.Code, setup.Guest.Id, CancellationToken.None)));

        Assert.Equal("host_required", exception.Code);
    }

    [Fact]
    public async Task ConcurrentPhraseSubmissionsAdvanceEarlyAndRemainAnonymous()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);

        await Task.WhenAll(
            WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
                setup.Code,
                setup.Host.Id,
                "First anonymous phrase",
                CancellationToken.None)),
            WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
                setup.Code,
                setup.Guest.Id,
                "Second anonymous phrase",
                CancellationToken.None)));

        await using var context = database.CreateDbContext();
        var round = await context.Rounds.Include(savedRound => savedRound.Phrases).SingleAsync();
        Assert.Equal(RoundPhase.PhraseVoting, round.Phase);
        Assert.Equal(2, round.Phrases.Count);

        using var scope = factory.Services.CreateScope();
        var publicSnapshot = scope.ServiceProvider.GetRequiredService<GameStateProjector>().CreatePhaseSnapshot(round);
        var json = JsonSerializer.Serialize(publicSnapshot, JsonOptions);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("displayName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("avatar", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnVoteIsRejectedAndTieSelectsOnlyALeadingPhrase()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);
        await SubmitBothPhrasesAsync(setup);
        var phrases = await LoadPhraseIdsAsync();

        var selfVote = await Assert.ThrowsAsync<ApiException>(() => WithCoordinatorAsync(coordinator =>
            coordinator.VotePhraseAsync(setup.Code, setup.Host.Id, phrases[setup.Host.Id], CancellationToken.None)));
        Assert.Equal("self_vote_forbidden", selfVote.Code);

        await WithCoordinatorAsync(coordinator => coordinator.VotePhraseAsync(
            setup.Code,
            setup.Host.Id,
            phrases[setup.Guest.Id],
            CancellationToken.None));
        await WithCoordinatorAsync(coordinator => coordinator.VotePhraseAsync(
            setup.Code,
            setup.Guest.Id,
            phrases[setup.Host.Id],
            CancellationToken.None));

        await using var context = database.CreateDbContext();
        var round = await context.Rounds.SingleAsync();
        Assert.Equal(RoundPhase.GifSubmission, round.Phase);
        Assert.True(round.SelectedPhraseId.HasValue);
        Assert.Contains(round.SelectedPhraseId.Value, phrases.Values);
        Assert.Equal(2, await context.PhraseVotes.CountAsync());
    }

    [Fact]
    public async Task SinglePhraseIsSelectedAutomaticallyAtTimeout()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);
        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Host.Id,
            "Only phrase",
            CancellationToken.None));
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(31);

        await WithCoordinatorAsync(async coordinator =>
        {
            await coordinator.ProcessExpiredRoundsAsync(CancellationToken.None);
            return true;
        });

        await using var context = database.CreateDbContext();
        var round = await context.Rounds.Include(savedRound => savedRound.Phrases).SingleAsync();
        Assert.Equal(RoundPhase.GifSubmission, round.Phase);
        Assert.Equal(Assert.Single(round.Phrases).Id, round.SelectedPhraseId);
    }

    [Fact]
    public async Task RoundWithoutPhrasesMovesToResultsWithoutPoints()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(31);

        await WithCoordinatorAsync(async coordinator =>
        {
            await coordinator.ProcessExpiredRoundsAsync(CancellationToken.None);
            return true;
        });

        await using var context = database.CreateDbContext();
        var round = await context.Rounds.SingleAsync();
        Assert.Equal(RoundPhase.Results, round.Phase);
        Assert.Null(round.SelectedPhraseId);
        Assert.All(await context.GamePlayers.ToArrayAsync(), player => Assert.Equal(0, player.Score));
    }

    [Fact]
    public async Task RepeatedConcurrentPhraseCommandIsIdempotent()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);

        await Task.WhenAll(
            WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
                setup.Code,
                setup.Host.Id,
                "Same phrase",
                CancellationToken.None)),
            WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
                setup.Code,
                setup.Host.Id,
                "Same phrase",
                CancellationToken.None)));

        await using var context = database.CreateDbContext();
        Assert.Equal(1, await context.Phrases.CountAsync());
        Assert.Equal(RoundPhase.PhraseSubmission, (await context.Rounds.SingleAsync()).Phase);
    }

    [Fact]
    public async Task SchedulerAdvancesAnExpiredPhrasePhase()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(31);

        RoundPhase phase = RoundPhase.PhraseSubmission;
        for (var attempt = 0; attempt < 30 && phase == RoundPhase.PhraseSubmission; attempt++)
        {
            await Task.Delay(100);
            await using var context = database.CreateDbContext();
            phase = await context.Rounds.Select(round => round.Phase).SingleAsync();
        }

        Assert.Equal(RoundPhase.Results, phase);
    }

    [Fact]
    public async Task DisconnectedPlayerDoesNotBlockEarlyAdvancement()
    {
        var setup = await CreateReadyGameAsync();
        await StartGameAsync(setup);
        await WithGameServiceAsync(async service =>
        {
            await service.DisconnectAsync(setup.Code, setup.Guest.Id, CancellationToken.None);
            return true;
        });

        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Host.Id,
            "Only connected player phrase",
            CancellationToken.None));

        await using var context = database.CreateDbContext();
        Assert.Equal(RoundPhase.GifSubmission, (await context.Rounds.SingleAsync()).Phase);
    }

    public void Dispose()
    {
        factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<GameSetup> CreateReadyGameAsync()
    {
        await database.ResetAsync();
        User[] users;
        await using (var context = database.CreateDbContext())
        {
            users =
            [
                CreateUser("round-host"),
                CreateUser("round-guest")
            ];
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        var created = await WithGameServiceAsync(service => service.CreateAsync(
            users[0].Id,
            3,
            CancellationToken.None));
        await WithGameServiceAsync(service => service.JoinAsync(
            created.Lobby.Code,
            users[1].Id,
            CancellationToken.None));
        await WithGameServiceAsync(service => service.SetReadyAsync(
            created.Lobby.Code,
            users[1].Id,
            true,
            CancellationToken.None));
        return new(created.Lobby.Code, users[0], users[1]);
    }

    private Task<PlayerGameSnapshot> StartGameAsync(GameSetup setup) =>
        WithCoordinatorAsync(coordinator => coordinator.StartGameAsync(
            setup.Code,
            setup.Host.Id,
            CancellationToken.None));

    private async Task SubmitBothPhrasesAsync(GameSetup setup)
    {
        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Host.Id,
            "Host phrase",
            CancellationToken.None));
        await WithCoordinatorAsync(coordinator => coordinator.SubmitPhraseAsync(
            setup.Code,
            setup.Guest.Id,
            "Guest phrase",
            CancellationToken.None));
    }

    private async Task<Dictionary<Guid, Guid>> LoadPhraseIdsAsync()
    {
        await using var context = database.CreateDbContext();
        return await context.Phrases.ToDictionaryAsync(phrase => phrase.UserId, phrase => phrase.Id);
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

    private sealed record GameSetup(string Code, User Host, User Guest);
}
