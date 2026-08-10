namespace GifJam.Api.GameEngine;

public sealed partial class GameRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<GameRecoveryWorker> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<GameRecoveryService>()
                .RecoverAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogRecoveryFailed(logger, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 4101, Level = LogLevel.Error, Message = "Active game recovery failed")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);
}
