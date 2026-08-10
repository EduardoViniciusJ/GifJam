using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;

namespace GifJam.Api.Tests.Security;

[Collection(PostgresTestGroup.Name)]
public sealed class AuthorizationAndErrorTests(PostgresFixture database)
{
    [Fact]
    public async Task GameGifAndHubSurfacesRejectAnonymousRequests()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });

        using var game = await client.GetAsync("/api/games/ABCDE");
        using var gifs = await client.GetAsync("/api/games/ABCDE/gifs/search?q=teste");
        using var hub = await client.PostAsync("/hubs/game/negotiate?negotiateVersion=1", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, game.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, gifs.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, hub.StatusCode);
    }

    [Fact]
    public async Task ProblemDetailsContainsMatchingTraceIdWithoutInternalDetails()
    {
        await database.ResetAsync();
        using var factory = new DiscordAuthFactory(database);
        var user = new User
        {
            DiscordId = "trace-user",
            Username = "trace-user",
            DisplayName = "Trace User",
            CreatedAt = factory.Clock.UtcNow,
            UpdatedAt = factory.Clock.UtcNow
        };
        var token = factory.CreateAccessToken(user);
        using var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/games/ZZZZZ");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var traceId = json.RootElement.GetProperty("traceId").GetString();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("game_not_found", json.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.DoesNotContain("Npgsql", json.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", json.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
