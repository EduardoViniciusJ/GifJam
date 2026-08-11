using GifJam.Api.Features.Games.Interfaces;

namespace GifJam.Api.GameEngine;

public sealed partial class RoundScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<RoundScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<IGameRoundService>();
                await coordinator.ProcessExpiredRoundsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSchedulerFailure(logger, exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    [LoggerMessage(EventId = 4000, Level = LogLevel.Error, Message = "Round scheduler iteration failed")]
    private static partial void LogSchedulerFailure(ILogger logger, Exception exception);
}
