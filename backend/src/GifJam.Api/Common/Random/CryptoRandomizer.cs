using System.Security.Cryptography;

namespace GifJam.Api.Common.Random;

public sealed class CryptoRandomizer : IRandomizer
{
    public int NextInt32(int exclusiveUpperBound) => RandomNumberGenerator.GetInt32(exclusiveUpperBound);

    public void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var selectedIndex = NextInt32(index + 1);
            (items[index], items[selectedIndex]) = (items[selectedIndex], items[index]);
        }
    }
}
