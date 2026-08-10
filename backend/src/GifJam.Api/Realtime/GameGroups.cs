namespace GifJam.Api.Realtime;

public static class GameGroups
{
    public static string ForCode(string gameCode) => $"game:{gameCode.ToUpperInvariant()}";
}
