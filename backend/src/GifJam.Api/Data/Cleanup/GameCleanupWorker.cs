using Microsoft.Extensions.Options;

namespace GifJam.Api.Data.Cleanup;

public sealed class GameCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GameRetentionOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<GameCleanupService>();
            await cleanupService.DeleteExpiredGamesAsync(stoppingToken);
        }
    }
}
