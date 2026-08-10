using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Features.Games;
using GifJam.Api.Realtime.Contracts;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GameHubTests(PostgresFixture database)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task TwoClientsReceiveLobbyUpdateAndCanRequestSync()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        var users = await SeedUsersAsync(factory);
        var hostToken = factory.CreateAccessToken(users[0]);
        var guestToken = factory.CreateAccessToken(users[1]);
        using var hostClient = CreateHttpClient(factory, hostToken);
        using var guestClient = CreateHttpClient(factory, guestToken);

        using var createResponse = await hostClient.PostAsJsonAsync("/api/games", new CreateGameRequest(3));
        var created = await createResponse.Content.ReadFromJsonAsync<PlayerGameSnapshot>(JsonOptions)
            ?? throw new InvalidOperationException("Created game snapshot was missing.");
        using var joinResponse = await guestClient.PostAsync(
            $"/api/games/{created.Lobby.Code}/join",
            content: null);
        joinResponse.EnsureSuccessStatusCode();

        await using var hostHub = CreateHub(factory, hostToken);
        await using var guestHub = CreateHub(factory, guestToken);
        var lobbyUpdated = new TaskCompletionSource<LobbySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateSynced = new TaskCompletionSource<PlayerGameSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var presenceChanged = new TaskCompletionSource<PresenceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandRejected = new TaskCompletionSource<CommandRejectedMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        hostHub.On<LobbySnapshot>("LobbyUpdated", snapshot => lobbyUpdated.TrySetResult(snapshot));
        hostHub.On<PlayerGameSnapshot>("StateSynced", snapshot => stateSynced.TrySetResult(snapshot));
        hostHub.On<PresenceSnapshot>("PresenceChanged", snapshot => presenceChanged.TrySetResult(snapshot));
        hostHub.On<CommandRejectedMessage>("CommandRejected", rejection => commandRejected.TrySetResult(rejection));

        await hostHub.StartAsync();
        await guestHub.StartAsync();
        await hostHub.InvokeAsync("SubscribeGame", created.Lobby.Code);
        await guestHub.InvokeAsync("SubscribeGame", created.Lobby.Code);
        var presence = await presenceChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, presence.Players.Count);
        await guestHub.InvokeAsync("SetReady", created.Lobby.Code, true);

        var lobby = await lobbyUpdated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(lobby.CanStart);
        Assert.All(lobby.Players, player => Assert.True(player.IsReady));

        await hostHub.InvokeAsync("RequestSync", created.Lobby.Code);
        var synced = await stateSynced.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(created.Lobby.Code, synced.Lobby.Code);
        Assert.True(synced.IsHost);

        await hostHub.InvokeAsync("RequestSync", "ZZZZZ");
        var rejection = await commandRejected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("game_not_found", rejection.Code);
    }

    private async Task<User[]> SeedUsersAsync(DiscordAuthFactory factory)
    {
        await using var context = database.CreateDbContext();
        var users = new[]
        {
            CreateUser("hub-host", factory.Clock.UtcNow),
            CreateUser("hub-guest", factory.Clock.UtcNow)
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        return users;
    }

    private static User CreateUser(string discordId, DateTimeOffset now) => new()
    {
        DiscordId = discordId,
        Username = discordId,
        DisplayName = discordId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static HttpClient CreateHttpClient(DiscordAuthFactory factory, string token)
    {
        var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HubConnection CreateHub(DiscordAuthFactory factory, string token) =>
        new HubConnectionBuilder()
            .WithUrl("https://api.test/hubs/game", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
