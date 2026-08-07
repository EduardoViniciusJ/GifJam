using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Gifs;
using GifJam.Api.GameEngine;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GifJam.Api.Tests.Gifs;

[Collection(PostgresTestGroup.Name)]
public sealed class GifSubmissionTests(PostgresFixture database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AuthenticatedParticipantSearchesNormalizedGifsWithoutExposingKey()
    {
        var provider = new StubGifProvider(CreateItem("gif-1"));
        using var factory = new DiscordAuthFactory(database, provider);
        var setup = await SeedGifSubmissionGameAsync(factory);
        using var client = CreateClient(factory, factory.CreateAccessToken(setup.Host));

        using var response = await client.GetAsync($"/api/games/{setup.Code}/gifs/search?q=feliz");
        var rawJson = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<GifSearchResponse>(rawJson, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(Assert.IsType<GifSearchResponse>(payload).Items);
        Assert.Equal("gif-1", item.Id);
        Assert.Equal("Search KLIPY", payload.SearchPlaceholder);
        Assert.NotEmpty(item.SelectionToken);
        Assert.DoesNotContain("test-klipy-key", rawJson, StringComparison.Ordinal);
        Assert.Equal("feliz", provider.LastQuery);
    }

    [Fact]
    public async Task GifSearchIsLimitedToTenRequestsPerMinutePerUser()
    {
        using var factory = new DiscordAuthFactory(database, new StubGifProvider(CreateItem("gif-1")));
        var setup = await SeedGifSubmissionGameAsync(factory);
        using var client = CreateClient(factory, factory.CreateAccessToken(setup.Host));

        for (var request = 0; request < 10; request++)
        {
            using var allowed = await client.GetAsync($"/api/games/{setup.Code}/gifs/search?q=teste");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var limited = await client.GetAsync($"/api/games/{setup.Code}/gifs/search?q=teste");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task GifSearchRejectsWrongPhaseAndProviderFailureIsIsolated()
    {
        using var factory = new DiscordAuthFactory(database, new StubGifProvider(new GifProviderUnavailableException("offline")));
        var setup = await SeedGifSubmissionGameAsync(factory);
        using var client = CreateClient(factory, factory.CreateAccessToken(setup.Host));

        using var unavailable = await client.GetAsync($"/api/games/{setup.Code}/gifs/search?q=teste");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);

        await using (var context = database.CreateDbContext())
        {
            var round = await context.Rounds.SingleAsync();
            round.Phase = RoundPhase.Results;
            await context.SaveChangesAsync();
        }

        using var wrongPhase = await client.GetAsync($"/api/games/{setup.Code}/gifs/search?q=teste");
        Assert.Equal(HttpStatusCode.Conflict, wrongPhase.StatusCode);
    }

    [Fact]
    public async Task ParticipantCanReplaceGifAndOnlySignedMetadataIsPersisted()
    {
        using var factory = new DiscordAuthFactory(database);
        var setup = await SeedGifSubmissionGameAsync(factory);
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<GifSelectionTokenService>();
        var coordinator = scope.ServiceProvider.GetRequiredService<GameCoordinator>();
        var firstToken = tokenService.Create(setup.Code, CreateItem("gif-1"));
        var secondToken = tokenService.Create(setup.Code, CreateItem("gif-2"));

        var firstSnapshot = await coordinator.SubmitGifAsync(
            setup.Code,
            setup.Host.Id,
            firstToken,
            CancellationToken.None);
        var secondSnapshot = await coordinator.SubmitGifAsync(
            setup.Code,
            setup.Host.Id,
            secondToken,
            CancellationToken.None);

        Assert.True(firstSnapshot.Round?.HasSubmittedGif);
        Assert.Equal("gif-2", secondSnapshot.Round?.GifSelection?.ExternalId);
        await using var context = database.CreateDbContext();
        var submission = await context.GifSubmissions.SingleAsync();
        Assert.Equal("gif-2", submission.ExternalId);
        Assert.Equal("https://static.klipy.test/gif-2.gif", submission.MediaUrl);
    }

    [Fact]
    public async Task CoordinatorRejectsTamperedAndCrossRoomTokens()
    {
        using var factory = new DiscordAuthFactory(database);
        var setup = await SeedGifSubmissionGameAsync(factory);
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<GifSelectionTokenService>();
        var coordinator = scope.ServiceProvider.GetRequiredService<GameCoordinator>();
        var token = tokenService.Create(setup.Code, CreateItem("gif-1"));
        var tampered = $"{token[..^1]}{(token[^1] == 'A' ? 'B' : 'A')}";

        var tamperedException = await Assert.ThrowsAsync<GifJam.Api.Common.Errors.ApiException>(() =>
            coordinator.SubmitGifAsync(setup.Code, setup.Host.Id, tampered, CancellationToken.None));
        var roomException = await Assert.ThrowsAsync<GifJam.Api.Common.Errors.ApiException>(() =>
            coordinator.SubmitGifAsync("FGHJK", setup.Host.Id, token, CancellationToken.None));

        Assert.Equal("invalid_gif_selection", tamperedException.Code);
        Assert.Equal("invalid_gif_selection", roomException.Code);
        await using var context = database.CreateDbContext();
        Assert.Empty(await context.GifSubmissions.ToArrayAsync());
    }

    private async Task<GameSetup> SeedGifSubmissionGameAsync(DiscordAuthFactory factory)
    {
        await database.ResetAsync();
        await using var context = database.CreateDbContext();
        var host = new User
        {
            DiscordId = "gif-host",
            Username = "gif-host",
            DisplayName = "GIF Host",
            CreatedAt = factory.Clock.UtcNow,
            UpdatedAt = factory.Clock.UtcNow
        };
        var guest = new User
        {
            DiscordId = "gif-guest",
            Username = "gif-guest",
            DisplayName = "GIF Guest",
            CreatedAt = factory.Clock.UtcNow,
            UpdatedAt = factory.Clock.UtcNow
        };
        var game = new Game
        {
            Code = "ABCDE",
            HostUserId = host.Id,
            HostUser = host,
            Status = GameStatus.InProgress,
            TotalRounds = 3,
            CurrentRoundNumber = 1,
            CreatedAt = factory.Clock.UtcNow,
            StartedAt = factory.Clock.UtcNow
        };
        game.Players.Add(new()
        {
            GameId = game.Id,
            Game = game,
            UserId = host.Id,
            User = host,
            IsReady = true,
            IsConnected = true,
            JoinedAt = factory.Clock.UtcNow,
            LastSeenAt = factory.Clock.UtcNow
        });
        game.Players.Add(new()
        {
            GameId = game.Id,
            Game = game,
            UserId = guest.Id,
            User = guest,
            IsReady = true,
            IsConnected = true,
            JoinedAt = factory.Clock.UtcNow,
            LastSeenAt = factory.Clock.UtcNow
        });
        game.Rounds.Add(new()
        {
            GameId = game.Id,
            Game = game,
            RoundNumber = 1,
            Phase = RoundPhase.GifSubmission,
            PhaseEndsAt = factory.Clock.UtcNow.AddMinutes(1),
            StartedAt = factory.Clock.UtcNow
        });
        context.Games.Add(game);
        await context.SaveChangesAsync();
        return new(game.Code, host);
    }

    private static HttpClient CreateClient(DiscordAuthFactory factory, string token)
    {
        var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static GifProviderItem CreateItem(string id) => new(
        id,
        $"Description for {id}",
        $"https://static.klipy.test/{id}-preview.gif",
        $"https://static.klipy.test/{id}.gif",
        480,
        270,
        240,
        135,
        $"https://klipy.test/gifs/{id}",
        "Powered by KLIPY");

    private sealed record GameSetup(string Code, User Host);

    private sealed class StubGifProvider : IGifProvider
    {
        private readonly GifProviderItem? item;
        private readonly Exception? exception;

        public StubGifProvider(GifProviderItem item) => this.item = item;

        public StubGifProvider(Exception exception) => this.exception = exception;

        public string? LastQuery { get; private set; }

        public Task<GifProviderSearchResult> SearchAsync(
            string query,
            string? cursor,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            if (exception is not null)
            {
                return Task.FromException<GifProviderSearchResult>(exception);
            }

            return Task.FromResult(new GifProviderSearchResult([item!], "next-cursor"));
        }
    }
}
