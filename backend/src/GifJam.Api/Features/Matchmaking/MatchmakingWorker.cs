using Microsoft.Extensions.Options;

namespace GifJam.Api.Features.Matchmaking;

public sealed partial class MatchmakingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MatchmakingOptions> options,
    ILogger<MatchmakingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.ProcessingIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider
                        .GetRequiredService<IMatchmakingService>()
                        .ProcessDueBatchesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    LogProcessingFailure(logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(EventId = 4301, Level = LogLevel.Error, Message = "Matchmaking processing iteration failed")]
    private static partial void LogProcessingFailure(ILogger logger, Exception exception);
}
