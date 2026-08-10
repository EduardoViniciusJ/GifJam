using System.Diagnostics.Metrics;
using GifJam.Api.Domain.Enums;

namespace GifJam.Api.GameEngine;

public sealed partial class GameTelemetry(ILogger<GameTelemetry> logger)
{
    public const string MeterName = "GifJam.Api.Game";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> GamesCreated = Meter.CreateCounter<long>("gifjam.games.created");
    private static readonly Counter<long> GamesStarted = Meter.CreateCounter<long>("gifjam.games.started");
    private static readonly Counter<long> GamesCompleted = Meter.CreateCounter<long>("gifjam.games.completed");
    private static readonly Counter<long> PhaseTransitions = Meter.CreateCounter<long>("gifjam.round.phase_transitions");
    private static readonly Histogram<double> GameDuration = Meter.CreateHistogram<double>(
        "gifjam.games.duration",
        unit: "s");

    public void GameCreated(string gameCode, int totalRounds)
    {
        GamesCreated.Add(1, new KeyValuePair<string, object?>("game.total_rounds", totalRounds));
        LogGameCreated(logger, gameCode, totalRounds);
    }

    public void GameStarted(string gameCode, int playerCount, int totalRounds)
    {
        GamesStarted.Add(1,
            new KeyValuePair<string, object?>("game.player_count", playerCount),
            new KeyValuePair<string, object?>("game.total_rounds", totalRounds));
        LogGameStarted(logger, gameCode, playerCount, totalRounds);
    }

    public void PhaseChanged(string gameCode, int roundNumber, RoundPhase phase)
    {
        PhaseTransitions.Add(1, new KeyValuePair<string, object?>("round.phase", phase.ToString()));
        LogPhaseChanged(logger, gameCode, roundNumber, phase);
    }

    public void GameFinished(string gameCode, int totalRounds, TimeSpan duration)
    {
        GamesCompleted.Add(1, new KeyValuePair<string, object?>("game.total_rounds", totalRounds));
        GameDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("game.total_rounds", totalRounds));
        LogGameFinished(logger, gameCode, totalRounds, duration.TotalSeconds);
    }

    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Game {GameCode} created with {TotalRounds} rounds")]
    private static partial void LogGameCreated(ILogger logger, string gameCode, int totalRounds);

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Game {GameCode} started with {PlayerCount} players and {TotalRounds} rounds")]
    private static partial void LogGameStarted(
        ILogger logger,
        string gameCode,
        int playerCount,
        int totalRounds);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Information,
        Message = "Game {GameCode} entered round {RoundNumber} phase {Phase}")]
    private static partial void LogPhaseChanged(
        ILogger logger,
        string gameCode,
        int roundNumber,
        RoundPhase phase);

    [LoggerMessage(
        EventId = 4203,
        Level = LogLevel.Information,
        Message = "Game {GameCode} finished after {TotalRounds} rounds in {DurationSeconds} seconds")]
    private static partial void LogGameFinished(
        ILogger logger,
        string gameCode,
        int totalRounds,
        double durationSeconds);
}
