namespace GifJam.Api.Features.Rooms;

public interface IRoomDirectoryRealtimeNotifier
{
    Task DirectoryChangedAsync(CancellationToken cancellationToken);
}
