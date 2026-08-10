using Microsoft.Extensions.Options;

namespace GifJam.Api.Data.Cleanup;

public sealed partial class GameCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GameRetentionOptions> options,
    ILogger<GameCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var cleanupService = scope.ServiceProvider.GetRequiredService<GameCleanupService>();
                    await cleanupService.DeleteExpiredGamesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogCleanupFailure(logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Game cleanup iteration failed")]
    private static partial void LogCleanupFailure(ILogger logger, Exception exception);
}
