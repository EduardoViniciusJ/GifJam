namespace GifJam.Api.Features.Matchmaking;

public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";

    public int BatchWindowSeconds { get; init; } = 30;

    public int ProcessingIntervalSeconds { get; init; } = 1;

    public int DefaultTotalRounds { get; init; } = 3;

    public int DefaultPhraseSubmissionSeconds { get; init; } = 60;

    public int DefaultResultsSeconds { get; init; } = 60;
}
