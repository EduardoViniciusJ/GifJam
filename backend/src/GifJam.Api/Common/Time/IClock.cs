namespace GifJam.Api.Common.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
