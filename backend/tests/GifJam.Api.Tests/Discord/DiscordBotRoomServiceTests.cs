using GifJam.Api.Common.Errors;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games.Interfaces;
using GifJam.Api.Integrations.Discord;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Discord;

[Collection(PostgresTestGroup.Name)]
public sealed class DiscordBotRoomServiceTests(PostgresFixture database)
{
    [Fact]
    public async Task CreatesPrivateLobbyAndProvisionsDiscordUser()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        var identity = new DiscordIdentity(
            "908172635445566778",
            "gifjam-player",
            "GifJam Player",
            "https://cdn.discord.test/player.png");

        var result = await CreateOrReuseAsync(factory, identity);

        Assert.False(result.WasReused);
        Assert.Equal(5, result.Code.Length);
        Assert.Equal(RoomVisibility.Private, result.Visibility);
        Assert.Equal(GameMode.Classic, result.Mode);

        await using var context = database.CreateDbContext();
        var user = await context.Users.SingleAsync();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .SingleAsync();
        var host = Assert.Single(game.Players);

        Assert.Equal(identity.DiscordId, user.DiscordId);
        Assert.Equal(identity.Username, user.Username);
        Assert.Equal(identity.DisplayName, user.DisplayName);
        Assert.Equal(identity.AvatarUrl, user.AvatarUrl);
        Assert.Equal(result.Code, game.Code);
        Assert.Equal(user.Id, game.HostUserId);
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Equal(RoomVisibility.Private, game.Visibility);
        Assert.Equal(GameMode.Classic, game.Mode);
        Assert.Equal(3, game.TotalRounds);
        Assert.Equal(60, game.PhraseSubmissionSeconds);
        Assert.Equal(60, game.ResultsSeconds);
        Assert.True(host.IsReady);
        Assert.False(host.IsConnected);
    }

    [Fact]
    public async Task ConcurrentCommandsReuseOneLobby()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        var identity = new DiscordIdentity(
            "118172635445566779",
            "concurrent-player",
            "Concurrent Player",
            null);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4)
                .Select(_ => CreateOrReuseAsync(factory, identity)));

        Assert.Single(results.Select(result => result.Code).Distinct());
        Assert.Single(results, result => !result.WasReused);
        Assert.Equal(3, results.Count(result => result.WasReused));

        await using var context = database.CreateDbContext();
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(1, await context.Games.CountAsync());
    }

    [Fact]
    public async Task ReusesLobbyWithoutOverwritingSettingsAndRefreshesProfile()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        const string discordId = "228172635445566770";
        var first = await CreateOrReuseAsync(
            factory,
            new(discordId, "old-name", "Old Name", null));

        await using (var context = database.CreateDbContext())
        {
            var game = await context.Games.SingleAsync();
            game.Visibility = RoomVisibility.Public;
            game.TotalRounds = 6;
            game.PhraseSubmissionSeconds = 90;
            game.ResultsSeconds = 30;
            await context.SaveChangesAsync();
        }

        var reused = await CreateOrReuseAsync(
            factory,
            new(discordId, "new-name", "New Name", "https://cdn.discord.test/new.png"));

        Assert.True(reused.WasReused);
        Assert.Equal(first.Code, reused.Code);
        Assert.Equal(RoomVisibility.Public, reused.Visibility);
        Assert.Equal(6, reused.TotalRounds);
        Assert.Equal(90, reused.PhraseSubmissionSeconds);
        Assert.Equal(30, reused.ResultsSeconds);

        await using var verificationContext = database.CreateDbContext();
        var savedUser = await verificationContext.Users.SingleAsync();
        var savedGame = await verificationContext.Games.SingleAsync();
        Assert.Equal("new-name", savedUser.Username);
        Assert.Equal("New Name", savedUser.DisplayName);
        Assert.Equal("https://cdn.discord.test/new.png", savedUser.AvatarUrl);
        Assert.Equal(RoomVisibility.Public, savedGame.Visibility);
        Assert.Equal(6, savedGame.TotalRounds);
        Assert.Equal(90, savedGame.PhraseSubmissionSeconds);
        Assert.Equal(30, savedGame.ResultsSeconds);
    }

    [Fact]
    public async Task FindsHostedLobbyWithoutCreatingAnotherRoom()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        const string discordId = "338172635445566771";

        Assert.Null(await FindHostedLobbyAsync(factory, discordId));
        var created = await CreateOrReuseAsync(
            factory,
            new(discordId, "room-player", "Room Player", null));

        var found = await FindHostedLobbyAsync(factory, discordId);

        Assert.NotNull(found);
        Assert.True(found.WasReused);
        Assert.Equal(created.Code, found.Code);
        await using var context = database.CreateDbContext();
        Assert.Equal(1, await context.Games.CountAsync());
    }

    [Fact]
    public async Task OnlyCommandCreatorCanCloseHostedLobby()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        const string hostDiscordId = "448172635445566772";
        var created = await CreateOrReuseAsync(
            factory,
            new(hostDiscordId, "host-player", "Host Player", null));
        var now = DateTimeOffset.UtcNow;
        var guest = new User
        {
            DiscordId = "558172635445566773",
            Username = "guest-player",
            DisplayName = "Guest Player",
            CreatedAt = now,
            UpdatedAt = now
        };

        await using (var context = database.CreateDbContext())
        {
            context.Users.Add(guest);
            await context.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
            await gameService.JoinAsync(created.Code, guest.Id, CancellationToken.None);
            var exception = await Assert.ThrowsAsync<ApiException>(() =>
                gameService.CloseAsync(created.Code, guest.Id, CancellationToken.None));
            Assert.Equal("host_required", exception.Code);
            Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        }

        Assert.Null(await CloseHostedLobbyAsync(factory, guest.DiscordId));
        await using (var context = database.CreateDbContext())
        {
            Assert.Equal(GameStatus.Lobby, (await context.Games.SingleAsync()).Status);
        }

        Assert.Equal(created.Code, await CloseHostedLobbyAsync(factory, hostDiscordId));

        await using var verificationContext = database.CreateDbContext();
        var closedGame = await verificationContext.Games
            .Include(game => game.Players)
            .SingleAsync();
        Assert.Equal(GameStatus.Closed, closedGame.Status);
        Assert.NotNull(closedGame.FinishedAt);
        Assert.All(closedGame.Players, player =>
        {
            Assert.False(player.IsConnected);
            Assert.NotNull(player.LeftAt);
        });
        Assert.Null(await FindHostedLobbyAsync(factory, hostDiscordId));
    }

    private static async Task<DiscordBotRoomResult> CreateOrReuseAsync(
        DiscordAuthFactory factory,
        DiscordIdentity identity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<DiscordBotRoomService>()
            .CreateOrReuseAsync(identity, CancellationToken.None);
    }

    private static async Task<DiscordBotRoomResult?> FindHostedLobbyAsync(
        DiscordAuthFactory factory,
        string discordUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<DiscordBotRoomService>()
            .FindHostedLobbyAsync(discordUserId, CancellationToken.None);
    }

    private static async Task<string?> CloseHostedLobbyAsync(
        DiscordAuthFactory factory,
        string discordUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<DiscordBotRoomService>()
            .CloseHostedLobbyAsync(discordUserId, CancellationToken.None);
    }
}
