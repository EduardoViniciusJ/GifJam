using GifJam.Api.Common.Time;
using GifJam.Api.Features.Rooms;
using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

public sealed class RoomDirectoryRealtimeNotifier(
    IHubContext<RoomDirectoryHub, IRoomDirectoryClient> hubContext,
    IClock clock) : IRoomDirectoryRealtimeNotifier
{
    public Task DirectoryChangedAsync(CancellationToken cancellationToken) =>
        hubContext.Clients.All
            .DirectoryChanged(new(clock.UtcNow))
            .WaitAsync(cancellationToken);
}
