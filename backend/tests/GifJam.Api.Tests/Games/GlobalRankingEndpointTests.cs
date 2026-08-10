using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Features.Games;
using GifJam.Api.Tests.Auth;
using GifJam.Api.Tests.Infrastructure;

namespace GifJam.Api.Tests.Games;

[Collection(PostgresTestGroup.Name)]
public sealed class GlobalRankingEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture database;
    private readonly DiscordAuthFactory factory;

    public GlobalRankingEndpointTests(PostgresFixture database)
    {
        this.database = database;
        factory = new(database);
    }

    [Fact]
    public async Task RankingRequiresAuthentication()
    {
        await database.ResetAsync();

        using var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        using var response = await client.GetAsync("/api/ranking");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RankingReturnsUsersOrderedByAccumulatedScoreWithSharedPositions()
    {
        await database.ResetAsync();
        var users = await SeedUsersAsync();

        using var client = CreateClient(users[1]);
        using var response = await client.GetAsync("/api/ranking");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ranking = await response.Content.ReadFromJsonAsync<GlobalRankingSnapshot>(JsonOptions);

        Assert.NotNull(ranking);
        Assert.Equal(["top", "tie-a", "tie-b"], ranking.Entries.Select(entry => entry.Username));
        Assert.Equal([1, 2, 2], ranking.Entries.Select(entry => entry.Position));
        Assert.Equal([12, 8, 8], ranking.Entries.Select(entry => entry.Score));
        Assert.True(ranking.Entries.Single(entry => entry.Username == "tie-a").IsCurrentUser);
    }

    public void Dispose()
    {
        factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HttpClient CreateClient(User user)
    {
        var client = factory.CreateClient(new() { BaseAddress = new("https://api.test") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateAccessToken(user));
        return client;
    }

    private async Task<User[]> SeedUsersAsync()
    {
        await using var context = database.CreateDbContext();
        var users = new[]
        {
            new User
            {
                DiscordId = "ranking-top",
                Username = "top",
                DisplayName = "Top",
                TotalScore = 12,
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            },
            new User
            {
                DiscordId = "ranking-tie-a",
                Username = "tie-a",
                DisplayName = "Tie A",
                TotalScore = 8,
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            },
            new User
            {
                DiscordId = "ranking-tie-b",
                Username = "tie-b",
                DisplayName = "Tie B",
                TotalScore = 8,
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            },
            new User
            {
                DiscordId = "ranking-zero",
                Username = "zero",
                DisplayName = "Zero",
                CreatedAt = factory.Clock.UtcNow,
                UpdatedAt = factory.Clock.UtcNow
            }
        };

        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        return users;
    }
}
