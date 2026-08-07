using GifJam.Api.Common.Random;

namespace GifJam.Api.Features.Games;

public sealed class GameCodeGenerator(IRandomizer randomizer) : IGameCodeGenerator
{
    private const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string Generate()
    {
        Span<char> code = stackalloc char[5];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = AllowedCharacters[randomizer.NextInt32(AllowedCharacters.Length)];
        }

        return new(code);
    }
}
