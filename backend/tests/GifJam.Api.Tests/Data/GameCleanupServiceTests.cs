using GifJam.Api.Common.Time;
using GifJam.Api.Data.Cleanup;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Data;

[Collection(PostgresTestGroup.Name)]
public sealed class GameCleanupServiceTests(PostgresFixture database)
{
    [Fact]
    public async Task CleanupDeletesExpiredGamesAndKeepsRecentGames()
    {
        await database.ResetAsync();
        var now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        var user = new User
        {
            DiscordId = "cleanup-user",
            Username = "cleanup",
            DisplayName = "Cleanup User",
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        };
        var expiredGame = CreateGame("OLD24", user, now.AddHours(-25));
        var recentGame = CreateGame("NEW24", user, now.AddHours(-23));

        await using (var setupContext = database.CreateDbContext())
        {
            setupContext.AddRange(user, expiredGame, recentGame);
            await setupContext.SaveChangesAsync();
        }

        await using (var cleanupContext = database.CreateDbContext())
        {
            var service = new GameCleanupService(
                cleanupContext,
                new FixedClock(now),
                Options.Create(new GameRetentionOptions { RetentionHours = 24 }),
                NullLogger<GameCleanupService>.Instance);

            Assert.Equal(1, await service.DeleteExpiredGamesAsync());
        }

        await using var assertionContext = database.CreateDbContext();
        Assert.False(await assertionContext.Games.AnyAsync(game => game.Id == expiredGame.Id));
        Assert.True(await assertionContext.Games.AnyAsync(game => game.Id == recentGame.Id));
        Assert.True(await assertionContext.Users.AnyAsync(savedUser => savedUser.Id == user.Id));
    }

    private static Game CreateGame(string code, User host, DateTimeOffset createdAt) => new()
    {
        Code = code,
        HostUserId = host.Id,
        HostUser = host,
        TotalRounds = 3,
        CreatedAt = createdAt
    };

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
