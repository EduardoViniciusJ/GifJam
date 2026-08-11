using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games;
using GifJam.Api.Features.Matchmaking;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class MatchmakingTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;
    private readonly List<HttpClient> clients = [];

    public MatchmakingTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
    }

    [Fact]
    public async Task FirstPlayerWaitsWithoutDeadlineAndSixthPlayerCreatesLobbyImmediately()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(6);

        MatchmakingSnapshot? firstSnapshot = null;
        DateTimeOffset? sharedDeadline = null;
        for (var index = 0; index < users.Length; index++)
        {
            if (index > 0)
            {
                factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(1);
            }

            var joinedAt = factory.Clock.UtcNow;
            using var response = await CreateClient(users[index]).PostAsync(
                "/api/matchmaking/join",
                content: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var snapshot = await ReadSnapshotAsync(response);
            if (index == 0)
            {
                firstSnapshot = snapshot;
                Assert.Null(snapshot.DeadlineAt);
            }
            else if (index == 1)
            {
                sharedDeadline = snapshot.DeadlineAt;
                Assert.Equal(30, (sharedDeadline - joinedAt)!.Value.TotalSeconds);
            }
            else if (index < users.Length - 1)
            {
                Assert.Equal(
                    sharedDeadline!.Value.ToUnixTimeMilliseconds(),
                    snapshot.DeadlineAt!.Value.ToUnixTimeMilliseconds());
            }

            if (index < users.Length - 1)
            {
                Assert.Equal(MatchmakingStatus.Waiting, snapshot.Status);
            }
            else
            {
                Assert.Equal(MatchmakingStatus.Matched, snapshot.Status);
                Assert.NotNull(snapshot.GameCode);
            }
        }

        Assert.NotNull(firstSnapshot);
        Assert.Equal(users[0].Id, firstSnapshot!.HostUserId);

        await using var context = database.CreateDbContext();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .SingleAsync();
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Equal(users[0].Id, game.HostUserId);
        Assert.Equal(6, game.Players.Count);
        Assert.Equal(1, game.Players.Count(player => player.IsReady));
    }

    [Fact]
    public async Task DueBatchWithTwoPlayersCreatesLobbyWhenWorkerProcessesDeadline()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(2);

        using var firstResponse = await CreateClient(users[0]).PostAsync("/api/matchmaking/join", content: null);
        using var secondResponse = await CreateClient(users[1]).PostAsync("/api/matchmaking/join", content: null);
        var secondSnapshot = await ReadSnapshotAsync(secondResponse);
        Assert.Equal(MatchmakingStatus.Waiting, secondSnapshot.Status);
        Assert.Equal(30, (secondSnapshot.DeadlineAt - factory.Clock.UtcNow)!.Value.TotalSeconds);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(29);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IMatchmakingService>()
            .ProcessDueBatchesAsync(CancellationToken.None);

        await using var contextBeforeDeadline = database.CreateDbContext();
        Assert.Empty(await contextBeforeDeadline.Games.ToListAsync());

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(1);
        await scope.ServiceProvider
            .GetRequiredService<IMatchmakingService>()
            .ProcessDueBatchesAsync(CancellationToken.None);

        await using var context = database.CreateDbContext();
        var game = await context.Games
            .Include(savedGame => savedGame.Players)
            .SingleAsync();
        Assert.Equal(GameStatus.Lobby, game.Status);
        Assert.Equal(users[0].Id, game.HostUserId);
        Assert.Equal(2, game.Players.Count);
    }

    [Fact]
    public async Task SinglePlayerRemainsInQueueWithoutDeadline()
    {
        await database.ResetAsync();
        var user = Assert.Single(await SeedUsersAsync(1));
        var client = CreateClient(user);

        using var response = await client.PostAsync("/api/matchmaking/join", content: null);
        var initial = await ReadSnapshotAsync(response);
        Assert.Null(initial.DeadlineAt);
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddMinutes(5);

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IMatchmakingService>()
            .ProcessDueBatchesAsync(CancellationToken.None);

        var status = await client.GetFromJsonAsync<MatchmakingSnapshot>(
            "/api/matchmaking/status",
            JsonOptions);
        Assert.NotNull(status);
        Assert.Equal(MatchmakingStatus.Waiting, status!.Status);
        Assert.Null(status.GameCode);
        Assert.Null(status.DeadlineAt);

        await using var context = database.CreateDbContext();
        Assert.Empty(await context.Games.ToListAsync());
    }

    [Fact]
    public async Task LeavingQueueRemovesTicketAndAllowsNewJoin()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(2);
        var firstClient = CreateClient(users[0]);
        var secondClient = CreateClient(users[1]);

        using var joined = await firstClient.PostAsync("/api/matchmaking/join", content: null);
        using var secondJoined = await secondClient.PostAsync("/api/matchmaking/join", content: null);
        Assert.NotNull((await ReadSnapshotAsync(secondJoined)).DeadlineAt);

        using var left = await firstClient.PostAsync("/api/matchmaking/leave", content: null);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);

        using var statusResponse = await firstClient.GetAsync("/api/matchmaking/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<MatchmakingSnapshot>(JsonOptions);
        Assert.NotNull(status);
        Assert.Equal(MatchmakingStatus.NotInQueue, status!.Status);

        using var remainingStatus = await secondClient.GetAsync("/api/matchmaking/status");
        var remaining = await ReadSnapshotAsync(remainingStatus);
        Assert.Equal(MatchmakingStatus.Waiting, remaining.Status);
        Assert.Equal(1, remaining.PlayerCount);
        Assert.Null(remaining.DeadlineAt);
    }

    [Fact]
    public async Task PlayersFromDifferentActiveLobbiesJoinTheSameQueue()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync(2);
        var firstClient = CreateClient(users[0]);
        var secondClient = CreateClient(users[1]);

        using var firstGameResponse = await firstClient.PostAsJsonAsync(
            "/api/games",
            new CreateGameRequest(3));
        using var secondGameResponse = await secondClient.PostAsJsonAsync(
            "/api/games",
            new CreateGameRequest(3));
        Assert.Equal(HttpStatusCode.Created, firstGameResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondGameResponse.StatusCode);

        using var firstJoinResponse = await firstClient.PostAsync("/api/matchmaking/join", content: null);
        var firstJoin = await ReadSnapshotAsync(firstJoinResponse);
        Assert.Equal(MatchmakingStatus.Waiting, firstJoin.Status);
        Assert.Equal(1, firstJoin.PlayerCount);
        Assert.Null(firstJoin.DeadlineAt);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(5);
        using var secondJoinResponse = await secondClient.PostAsync("/api/matchmaking/join", content: null);
        var secondJoin = await ReadSnapshotAsync(secondJoinResponse);
        Assert.Equal(MatchmakingStatus.Waiting, secondJoin.Status);
        Assert.Equal(2, secondJoin.PlayerCount);
        Assert.Equal(30, (secondJoin.DeadlineAt - factory.Clock.UtcNow)!.Value.TotalSeconds);

        await using var context = database.CreateDbContext();
        Assert.Equal(2, await context.Games.CountAsync(game => game.Status == GameStatus.Closed));
        Assert.Empty(await context.Games.Where(game => game.Status == GameStatus.Lobby).ToListAsync());
        Assert.Single(await context.MatchmakingBatches.Where(batch => batch.Status == MatchmakingBatchStatus.Waiting).ToListAsync());
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
                DiscordId = $"matchmaking-user-{index}",
                Username = $"matchmaking-player-{index}",
                DisplayName = $"Matchmaking Player {index}",
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            })
            .ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        return users;
    }

    private static async Task<MatchmakingSnapshot> ReadSnapshotAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<MatchmakingSnapshot>(JsonOptions)
        ?? throw new InvalidOperationException("Matchmaking snapshot was missing.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
