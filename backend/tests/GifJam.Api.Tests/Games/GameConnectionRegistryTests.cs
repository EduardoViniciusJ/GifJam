using GifJam.Api.Realtime;

namespace GifJam.Api.Tests.Games;

public sealed class GameConnectionRegistryTests
{
    [Fact]
    public void AnotherConnectionKeepsUserPresentInGame()
    {
        var registry = new GameConnectionRegistry();
        var userId = Guid.CreateVersion7();
        registry.Track("connection-1", userId, "ABCDE");
        registry.Track("connection-2", userId, "ABCDE");

        Assert.True(registry.HasActiveConnection(userId, "ABCDE"));
    }
}
