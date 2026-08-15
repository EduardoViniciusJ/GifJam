using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Rooms;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class RoomDirectoryEndpointTests(PostgresFixture database)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task PublicDirectoryFiltersUnavailableRoomsAndDoesNotRequireAuthentication()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        using (var startup = await client.GetAsync("/health/live"))
        {
            startup.EnsureSuccessStatusCode();
        }

        var now = factory.Clock.UtcNow;
        await using (var context = database.CreateDbContext())
        {
            AddRoom(context, "POP01", RoomVisibility.Public, GameStatus.Lobby, 3, true, now.AddMinutes(-10));
            AddRoom(context, "NEW01", RoomVisibility.Public, GameStatus.Lobby, 1, true, now);
            AddRoom(context, "PRV01", RoomVisibility.Private, GameStatus.Lobby, 2, true, now);
            AddRoom(context, "FUL01", RoomVisibility.Public, GameStatus.Lobby, 6, true, now);
            AddRoom(context, "RUN01", RoomVisibility.Public, GameStatus.InProgress, 2, true, now);
            AddRoom(context, "OFF01", RoomVisibility.Public, GameStatus.Lobby, 2, false, now);
            await context.SaveChangesAsync();
        }

        using var popularResponse = await client.GetAsync(
            "/api/rooms/public?sort=popular&page=1&pageSize=20");
        var popular = await ReadDirectoryAsync(popularResponse);

        Assert.Equal(HttpStatusCode.OK, popularResponse.StatusCode);
        Assert.Equal(2, popular.Total);
        Assert.Equal(["POP01", "NEW01"], popular.Items.Select(room => room.Code));
        Assert.All(popular.Items, room => Assert.Equal(6, room.Capacity));

        using var recentResponse = await client.GetAsync(
            "/api/rooms/public?sort=recent&page=1&pageSize=1");
        var recent = await ReadDirectoryAsync(recentResponse);
        Assert.Equal(2, recent.Total);
        Assert.Equal("NEW01", Assert.Single(recent.Items).Code);
    }

    [Theory]
    [InlineData("/api/rooms/public?page=0")]
    [InlineData("/api/rooms/public?pageSize=51")]
    [InlineData("/api/rooms/public?sort=unknown")]
    public async Task PublicDirectoryRejectsInvalidQuery(string path)
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void AddRoom(
        GifJam.Api.Data.AppDbContext context,
        string code,
        RoomVisibility visibility,
        GameStatus status,
        int playerCount,
        bool connected,
        DateTimeOffset createdAt)
    {
        var users = Enumerable.Range(1, playerCount)
            .Select(index => new User
            {
                DiscordId = $"{code}-discord-{index}",
                Username = $"{code}-player-{index}",
                DisplayName = $"{code} Player {index}",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            })
            .ToArray();
        var game = new Game
        {
            Code = code,
            HostUserId = users[0].Id,
            Visibility = visibility,
            Status = status,
            Mode = GameMode.Classic,
            TotalRounds = 3,
            CreatedAt = createdAt
        };
        for (var index = 0; index < users.Length; index++)
        {
            game.Players.Add(new()
            {
                UserId = users[index].Id,
                JoinedAt = createdAt.AddSeconds(index),
                LastSeenAt = createdAt,
                IsConnected = connected && index == 0,
                IsReady = index == 0
            });
        }

        context.Users.AddRange(users);
        context.Games.Add(game);
    }

    private static async Task<PublicRoomDirectoryResponse> ReadDirectoryAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<PublicRoomDirectoryResponse>(JsonOptions)
        ?? throw new InvalidOperationException("Room directory response was missing.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
