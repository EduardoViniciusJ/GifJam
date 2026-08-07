namespace GifJam.Api.Common.Random;

public interface IRandomizer
{
    int NextInt32(int exclusiveUpperBound);

    void Shuffle<T>(IList<T> items);
}
