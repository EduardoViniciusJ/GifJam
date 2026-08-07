using GifJam.Api.Features.Games;

namespace GifJam.Api.Realtime;

public interface IGameRealtimeNotifier
{
    Task LobbyUpdatedAsync(string gameCode, LobbySnapshot snapshot, CancellationToken cancellationToken);

    Task PresenceChangedAsync(string gameCode, PresenceSnapshot snapshot, CancellationToken cancellationToken);
}
