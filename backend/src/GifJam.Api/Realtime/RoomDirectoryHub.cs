using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

public sealed class RoomDirectoryHub : Hub<IRoomDirectoryClient>;
