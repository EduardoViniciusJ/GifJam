using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GifJam.Api.Common.Errors;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Features.Auth;
using GifJam.Api.Integrations.Discord;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Tests.Auth;

[Collection(PostgresTestGroup.Name)]
public sealed class AuthEndpointTests : IDisposable
{
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;
    private readonly HttpClient client;

    public AuthEndpointTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
        client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new("https://api.test")
        });
    }

    [Fact]
    public async Task DiscordLoginCreatesUserAndOneTimeJwtExchange()
    {
        await database.ResetAsync();
        var state = await StartAndReadStateAsync("/rooms/ABCDE");

        using var callback = await client.GetAsync(
            $"/api/auth/discord/callback?code=valid-code&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var callbackQuery = QueryHelpers.ParseQuery(callback.Headers.Location!.Query);
        var exchangeCode = Assert.Single(callbackQuery["code"])
            ?? throw new InvalidOperationException("Exchange code was missing.");
        Assert.Equal("/rooms/ABCDE", Assert.Single(callbackQuery["returnUrl"]));

        using var exchange = await client.PostAsJsonAsync("/api/auth/exchange", new AuthExchangeRequest(exchangeCode));
        var auth = await exchange.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Equal("123456789012345678", auth.User.DiscordId);

        using var reusedExchange = await client.PostAsJsonAsync(
            "/api/auth/exchange",
            new AuthExchangeRequest(exchangeCode));
        Assert.Equal(HttpStatusCode.BadRequest, reusedExchange.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var meResponse = await client.GetAsync("/api/auth/me");
        var me = await meResponse.Content.ReadFromJsonAsync<AuthUserResponse>();
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(auth.User, me);
    }

    [Fact]
    public async Task InvalidStateDoesNotCreateUser()
    {
        await database.ResetAsync();

        using var response = await client.GetAsync(
            "/api/auth/discord/callback?code=valid-code&state=invalid-state");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = database.CreateDbContext();
        Assert.False(await context.Users.AnyAsync());
    }

    [Fact]
    public async Task DeniedDiscordCallbackDoesNotCreateUser()
    {
        await database.ResetAsync();
        var state = await StartAndReadStateAsync("/");

        using var response = await client.GetAsync(
            $"/api/auth/discord/callback?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("error=access_denied", response.Headers.Location!.Query, StringComparison.Ordinal);
        await using var context = database.CreateDbContext();
        Assert.False(await context.Users.AnyAsync());
    }

    [Fact]
    public async Task DiscordExchangeFailureReturnsToTheFrontendCallback()
    {
        await database.ResetAsync();
        using var failingFactory = new DiscordAuthFactory(
            database,
            discordClient: new FailingDiscordClient());
        using var failingClient = failingFactory.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new("https://api.test")
        });

        using var start = await failingClient.GetAsync(
            "/api/auth/discord/start?returnUrl=%2Fsala%2Fnova");
        var state = Assert.Single(QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"]);
        using var callback = await failingClient.GetAsync(
            $"/api/auth/discord/callback?code=expired-code&state={Uri.EscapeDataString(state!)}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var callbackQuery = QueryHelpers.ParseQuery(callback.Headers.Location!.Query);
        Assert.Equal("discord_exchange_failed", Assert.Single(callbackQuery["error"]));
        Assert.Equal("/sala/nova", Assert.Single(callbackQuery["returnUrl"]));
    }

    [Fact]
    public async Task ExpiredExchangeCodeIsRejected()
    {
        await database.ResetAsync();
        const string exchangeCode = "expired-code";
        await using (var context = database.CreateDbContext())
        {
            var user = new User
            {
                DiscordId = "expired-user",
                Username = "expired",
                DisplayName = "Expired User",
                CreatedAt = factory.Clock.UtcNow.AddMinutes(-2),
                UpdatedAt = factory.Clock.UtcNow.AddMinutes(-2)
            };
            context.AuthExchangeCodes.Add(new()
            {
                CodeHash = AuthTestHash.Create(exchangeCode),
                User = user,
                UserId = user.Id,
                ExpiresAt = factory.Clock.UtcNow.AddSeconds(-1)
            });
            await context.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/auth/exchange",
            new AuthExchangeRequest(exchangeCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExternalReturnUrlIsRejected()
    {
        using var response = await client.GetAsync(
            "/api/auth/discord/start?returnUrl=https%3A%2F%2Fevil.test");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RepeatedLoginUpdatesTheExistingDiscordUser()
    {
        await database.ResetAsync();
        var firstState = await StartAndReadStateAsync("/");
        using var firstCallback = await client.GetAsync(
            $"/api/auth/discord/callback?code=first-name&state={Uri.EscapeDataString(firstState)}");
        Assert.Equal(HttpStatusCode.Redirect, firstCallback.StatusCode);

        var secondState = await StartAndReadStateAsync("/");
        using var secondCallback = await client.GetAsync(
            $"/api/auth/discord/callback?code=updated-name&state={Uri.EscapeDataString(secondState)}");
        Assert.Equal(HttpStatusCode.Redirect, secondCallback.StatusCode);

        await using var context = database.CreateDbContext();
        var user = await context.Users.SingleAsync();
        Assert.Equal("user-updated-name", user.Username);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<string> StartAndReadStateAsync(string returnUrl)
    {
        using var response = await client.GetAsync(
            $"/api/auth/discord/start?returnUrl={Uri.EscapeDataString(returnUrl)}");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        return Assert.Single(query["state"])
            ?? throw new InvalidOperationException("OAuth state was missing.");
    }

    private sealed class FailingDiscordClient : IDiscordClient
    {
        public Task<DiscordIdentity> GetIdentityAsync(
            string authorizationCode,
            CancellationToken cancellationToken) =>
            throw new ApiException(
                "discord_exchange_failed",
                "Discord authentication could not be completed.",
                StatusCodes.Status502BadGateway);
    }
}
