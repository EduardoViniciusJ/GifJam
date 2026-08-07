using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GameRecoveryTests(PostgresFixture database)
{
    [Theory]
    [InlineData(RoundPhase.PhraseSubmission, RoundPhase.Results, 1)]
    [InlineData(RoundPhase.PhraseVoting, RoundPhase.Results, 1)]
    [InlineData(RoundPhase.GifSubmission, RoundPhase.Results, 1)]
    [InlineData(RoundPhase.GifVoting, RoundPhase.Results, 1)]
    [InlineData(RoundPhase.Results, RoundPhase.PhraseSubmission, 2)]
    public async Task ApiStartupRecoversEveryExpiredPhase(
        RoundPhase initialPhase,
        RoundPhase expectedPhase,
        int expectedRoundNumber)
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        await SeedGameAsync(
            factory.Clock.UtcNow,
            initialPhase,
            factory.Clock.UtcNow.AddMinutes(-1));

        using var client = factory.CreateClient();

        await using var context = database.CreateDbContext();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .Include(savedGame => savedGame.Rounds)
            .SingleAsync();
        Assert.Equal(expectedRoundNumber, game.CurrentRoundNumber);
        Assert.Equal(expectedPhase, game.Rounds.Single(round => round.RoundNumber == expectedRoundNumber).Phase);
        Assert.All(game.Players, player => Assert.False(player.IsConnected));
    }

    [Theory]
    [InlineData("none", false, false, false)]
    [InlineData("phrase", true, false, false)]
    [InlineData("gif", false, true, false)]
    [InlineData("vote", false, true, true)]
    public async Task ReconnectRestoresCompletedActionWithoutDuplicatingIt(
        string action,
        bool hasPhrase,
        bool hasGif,
        bool hasGifVote)
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        var setup = await SeedActionStateAsync(factory.Clock.UtcNow, action);
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<GameService>();

        var connected = await service.ConnectAsync(setup.Code, setup.HostId, CancellationToken.None);
        Assert.Equal(hasPhrase, connected.Round?.HasSubmittedPhrase);
        Assert.Equal(hasGif, connected.Round?.HasSubmittedGif);
        Assert.Equal(hasGifVote, connected.Round?.HasVotedGif);
        Assert.Contains(connected.Lobby.Players, player => player.UserId == setup.HostId && player.IsConnected);

        await service.DisconnectAsync(setup.Code, setup.HostId, CancellationToken.None);
        var disconnected = await service.GetAsync(setup.Code, setup.HostId, CancellationToken.None);
        Assert.Contains(disconnected.Lobby.Players, player => player.UserId == setup.HostId && !player.IsConnected);
        Assert.Equal(GameStatus.InProgress, disconnected.Lobby.Status);

        var reconnected = await service.ConnectAsync(setup.Code, setup.HostId, CancellationToken.None);
        Assert.Equal(hasPhrase, reconnected.Round?.HasSubmittedPhrase);
        Assert.Equal(hasGif, reconnected.Round?.HasSubmittedGif);
        Assert.Equal(hasGifVote, reconnected.Round?.HasVotedGif);
        await using var verification = database.CreateDbContext();
        Assert.Equal(hasPhrase ? 1 : 0, await verification.Phrases.CountAsync());
        Assert.Equal(hasGif ? 2 : 0, await verification.GifSubmissions.CountAsync());
        Assert.Equal(hasGifVote ? 1 : 0, await verification.GifVotes.CountAsync());
    }

    private async Task SeedGameAsync(
        DateTimeOffset now,
        RoundPhase phase,
        DateTimeOffset phaseEndsAt)
    {
        await using var context = database.CreateDbContext();
        var host = CreateUser("recovery-host", now);
        var game = CreateGame(host, now, phase, phaseEndsAt);
        context.Games.Add(game);
        await context.SaveChangesAsync();
    }

    private async Task<ActionSetup> SeedActionStateAsync(DateTimeOffset now, string action)
    {
        await using var context = database.CreateDbContext();
        var host = CreateUser("action-host", now);
        var guest = CreateUser("action-guest", now);
        var phase = action switch
        {
            "phrase" => RoundPhase.PhraseSubmission,
            "gif" => RoundPhase.GifSubmission,
            "vote" => RoundPhase.GifVoting,
            _ => RoundPhase.PhraseSubmission
        };
        var game = CreateGame(host, now, phase, now.AddMinutes(5));
        game.Players.Add(CreatePlayer(game, guest, now));
        var round = game.Rounds.Single();

        if (action == "phrase")
        {
            round.Phrases.Add(new()
            {
                RoundId = round.Id,
                Round = round,
                UserId = host.Id,
                User = host,
                Text = "Persisted phrase",
                SubmittedAt = now
            });
        }

        if (action is "gif" or "vote")
        {
            round.GifSubmissions.Add(CreateGifSubmission(round, host, "host-gif", now));
            round.GifSubmissions.Add(CreateGifSubmission(round, guest, "guest-gif", now));
        }

        if (action == "vote")
        {
            var guestGif = round.GifSubmissions.Single(submission => submission.UserId == guest.Id);
            round.GifVotes.Add(new()
            {
                RoundId = round.Id,
                Round = round,
                GifSubmissionId = guestGif.Id,
                GifSubmission = guestGif,
                UserId = host.Id,
                User = host,
                CreatedAt = now
            });
        }

        context.Games.Add(game);
        await context.SaveChangesAsync();
        return new(game.Code, host.Id);
    }

    private static Game CreateGame(
        User host,
        DateTimeOffset now,
        RoundPhase phase,
        DateTimeOffset phaseEndsAt)
    {
        var game = new Game
        {
            Code = "RCVRY",
            HostUserId = host.Id,
            HostUser = host,
            Status = GameStatus.InProgress,
            TotalRounds = 3,
            CurrentRoundNumber = 1,
            CreatedAt = now,
            StartedAt = now
        };
        game.Players.Add(CreatePlayer(game, host, now));
        game.Rounds.Add(new()
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = 1,
            Phase = phase,
            PhaseEndsAt = phaseEndsAt,
            StartedAt = now
        });
        return game;
    }

    private static GamePlayer CreatePlayer(Game game, User user, DateTimeOffset now) => new()
    {
        GameId = game.Id,
        Game = game,
        UserId = user.Id,
        User = user,
        IsReady = true,
        IsConnected = true,
        JoinedAt = now,
        LastSeenAt = now
    };

    private static GifSubmission CreateGifSubmission(Round round, User user, string id, DateTimeOffset now) => new()
    {
        RoundId = round.Id,
        Round = round,
        UserId = user.Id,
        User = user,
        Provider = "klipy",
        ExternalId = id,
        Description = id,
        PreviewUrl = $"https://static.klipy.test/{id}-preview.gif",
        MediaUrl = $"https://static.klipy.test/{id}.gif",
        SourceUrl = $"https://klipy.test/gifs/{id}",
        Attribution = "Powered by KLIPY",
        Width = 480,
        Height = 270,
        PreviewWidth = 240,
        PreviewHeight = 135,
        SubmittedAt = now
    };

    private static User CreateUser(string discordId, DateTimeOffset now) => new()
    {
        DiscordId = discordId,
        Username = discordId,
        DisplayName = discordId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed record ActionSetup(string Code, Guid HostId);
}
