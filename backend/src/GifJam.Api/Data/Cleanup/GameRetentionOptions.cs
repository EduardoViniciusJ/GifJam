namespace GifJam.Api.Data.Cleanup;

public sealed class GameRetentionOptions
{
    public const string SectionName = "GameRetention";

    public int RetentionHours { get; set; } = 24;

    public int CleanupIntervalMinutes { get; set; } = 60;
}
