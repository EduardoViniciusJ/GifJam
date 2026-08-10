using GifJam.Api.Common.Time;
using GifJam.Api.Common.Auth;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Integrations.Discord;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GifJam.Api.Tests.Auth;

public sealed class DiscordAuthFactory(
    PostgresFixture database,
    IGifProvider? gifProvider = null,
    IDiscordClient? discordClient = null) : WebApplicationFactory<Program>
{
    public TestClock Clock { get; } = new(DateTimeOffset.UtcNow);

    public string CreateAccessToken(User user)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<JwtTokenService>().Create(user).AccessToken;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:ClientId"] = "discord-test-client",
                ["Discord:ClientSecret"] = "discord-test-secret",
                ["Discord:CallbackUrl"] = "https://api.test/api/auth/discord/callback",
                ["Discord:AuthorizationEndpoint"] = "https://discord.test/oauth2/authorize",
                ["Jwt:SigningKey"] = new string('a', 64),
                ["Jwt:Issuer"] = "GifJam.Tests",
                ["Jwt:Audience"] = "GifJam.Tests.Client",
                ["Klipy:ApiKey"] = "test-klipy-key",
                ["ApplicationUrls:FrontendUrl"] = "https://frontend.test"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(database.ConnectionString));

            services.RemoveAll<IDiscordClient>();
            services.AddSingleton(discordClient ?? new FakeDiscordClient());
            services.RemoveAll<IGifProvider>();
            services.AddSingleton(gifProvider ?? new FakeGifProvider());
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    private sealed class FakeDiscordClient : IDiscordClient
    {
        public Task<DiscordIdentity> GetIdentityAsync(
            string authorizationCode,
            CancellationToken cancellationToken)
        {
            var identity = new DiscordIdentity(
                "123456789012345678",
                $"user-{authorizationCode}",
                "GifJam Tester",
                "https://cdn.discord.test/avatar.png");
            return Task.FromResult(identity);
        }
    }

    private sealed class FakeGifProvider : IGifProvider
    {
        public Task<GifProviderSearchResult> SearchAsync(
            string query,
            string? cursor,
            CancellationToken cancellationToken) =>
            Task.FromResult(new GifProviderSearchResult([], null));
    }
}

public sealed class TestClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}
