namespace GifJam.Api.Realtime.Contracts;

public interface IRoomDirectoryClient
{
    Task DirectoryChanged(RoomDirectoryChangedMessage message);
}

public sealed record RoomDirectoryChangedMessage(DateTimeOffset ServerTime);
