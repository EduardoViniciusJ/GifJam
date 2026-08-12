using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GameEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;
    private readonly List<HttpClient> clients = [];

    public GameEndpointTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
    }

    [Fact]
    public async Task RoomAcceptsSixPlayersAndRejectsTheSeventh()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(7);
        var hostClient = CreateClient(users[0]);
        var created = await CreateGameAsync(hostClient);

        for (var index = 1; index < 6; index++)
        {
            using var joined = await CreateClient(users[index]).PostAsync(
                $"/api/games/{created.Lobby.Code}/join",
                content: null);
            Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        }

        using var rejected = await CreateClient(users[6]).PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        using var response = await hostClient.GetAsync($"/api/games/{created.Lobby.Code}");
        var snapshot = await ReadSnapshotAsync(response);
        Assert.Equal(6, snapshot.Lobby.Players.Count);
        Assert.DoesNotContain('0', snapshot.Lobby.Code);
        Assert.DoesNotContain('O', snapshot.Lobby.Code);
        Assert.DoesNotContain('1', snapshot.Lobby.Code);
        Assert.DoesNotContain('I', snapshot.Lobby.Code);
    }

    [Fact]
    public async Task StartedRoomAllowsExistingMemberButRejectsNewPlayer()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(3);
        var created = await CreateGameAsync(CreateClient(users[0]));
        using var initialJoin = await CreateClient(users[1]).PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);
        Assert.Equal(HttpStatusCode.OK, initialJoin.StatusCode);

        await using (var context = database.CreateDbContext())
        {
            var game = await context.Games.SingleAsync();
            game.Status = GameStatus.InProgress;
            await context.SaveChangesAsync();
        }

        using var returningMember = await CreateClient(users[1]).PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);
        using var newMember = await CreateClient(users[2]).PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);

        Assert.Equal(HttpStatusCode.OK, returningMember.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, newMember.StatusCode);
    }

    [Fact]
    public async Task HostLeavingLobbyClosesTheRoom()
    {
        await database.ResetAsync();
        var host = Assert.Single(await SeedUsersAsync(1));
        var hostClient = CreateClient(host);
        var created = await CreateGameAsync(hostClient);

        using var leave = await hostClient.PostAsync($"/api/games/{created.Lobby.Code}/leave", content: null);
        using var get = await hostClient.GetAsync($"/api/games/{created.Lobby.Code}");

        Assert.Equal(HttpStatusCode.NoContent, leave.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task HostLeavingTransfersOwnershipAndLastPlayerClosesTheRoom()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(2);
        var hostClient = CreateClient(users[0]);
        var guestClient = CreateClient(users[1]);
        var created = await CreateGameAsync(hostClient);
        using var joined = await guestClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);
        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);

        using var hostLeave = await hostClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/leave",
            content: null);
        using var guestSnapshotResponse = await guestClient.GetAsync(
            $"/api/games/{created.Lobby.Code}");
        using var formerHostSnapshot = await hostClient.GetAsync(
            $"/api/games/{created.Lobby.Code}");

        Assert.Equal(HttpStatusCode.NoContent, hostLeave.StatusCode);
        Assert.Equal(HttpStatusCode.OK, guestSnapshotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, formerHostSnapshot.StatusCode);
        var guestSnapshot = await ReadSnapshotAsync(guestSnapshotResponse);
        var remainingPlayer = Assert.Single(guestSnapshot.Lobby.Players);
        Assert.Equal(users[1].Id, remainingPlayer.UserId);
        Assert.True(remainingPlayer.IsHost);
        Assert.True(remainingPlayer.IsReady);

        using var guestLeave = await guestClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/leave",
            content: null);
        using var closedRoom = await guestClient.GetAsync($"/api/games/{created.Lobby.Code}");
        Assert.Equal(HttpStatusCode.NoContent, guestLeave.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, closedRoom.StatusCode);

        await using var context = database.CreateDbContext();
        var game = await context.Games.Include(savedGame => savedGame.Players).SingleAsync();
        Assert.Equal(GameStatus.Closed, game.Status);
        Assert.All(game.Players, player => Assert.NotNull(player.LeftAt));
    }

    [Fact]
    public async Task LeavingStartedGameRemovesPlayerAndPreventsRejoin()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(2);
        var hostClient = CreateClient(users[0]);
        var guestClient = CreateClient(users[1]);
        var created = await CreateGameAsync(hostClient);
        using var joined = await guestClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);

        await using (var context = database.CreateDbContext())
        {
            var game = await context.Games.Include(savedGame => savedGame.Players).SingleAsync();
            game.Status = GameStatus.InProgress;
            foreach (var player in game.Players)
            {
                player.IsReady = true;
            }

            await context.SaveChangesAsync();
        }

        using var hostLeave = await hostClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/leave",
            content: null);
        using var guestSnapshotResponse = await guestClient.GetAsync(
            $"/api/games/{created.Lobby.Code}");
        using var formerHostRejoin = await hostClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, hostLeave.StatusCode);
        Assert.Equal(HttpStatusCode.OK, guestSnapshotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, formerHostRejoin.StatusCode);
        var guestSnapshot = await ReadSnapshotAsync(guestSnapshotResponse);
        Assert.Equal(users[1].Id, guestSnapshot.Lobby.HostUserId);
        Assert.Single(guestSnapshot.Lobby.Players);
    }

    [Fact]
    public async Task UnknownRoomReturnsNotFound()
    {
        await database.ResetAsync();
        var user = Assert.Single(await SeedUsersAsync(1));

        using var response = await CreateClient(user).GetAsync("/api/games/ZZZZZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<PlayerGameSnapshot> CreateGameAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/games", new CreateGameRequest(3));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadSnapshotAsync(response);
    }

    private HttpClient CreateClient(User user)
    {
        var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateAccessToken(user));
        clients.Add(client);
        return client;
    }

    private async Task<User[]> SeedUsersAsync(int count)
    {
        await using var context = database.CreateDbContext();
        var users = Enumerable.Range(1, count)
            .Select(index => new User
            {
                DiscordId = $"game-user-{index}",
                Username = $"player-{index}",
                DisplayName = $"Player {index}",
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            })
            .ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        return users;
    }

    private static async Task<PlayerGameSnapshot> ReadSnapshotAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<PlayerGameSnapshot>(JsonOptions)
        ?? throw new InvalidOperationException("Game snapshot was missing.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
