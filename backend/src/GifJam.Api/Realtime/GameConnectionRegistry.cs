using System.Collections.Concurrent;

namespace GifJam.Api.Realtime;

public sealed class GameConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConnectionRegistration> connections = new();

    public void Track(string connectionId, Guid userId, string gameCode)
    {
        connections.AddOrUpdate(
            connectionId,
            _ => new(userId, new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                [gameCode] = 0
            }),
            (_, registration) =>
            {
                registration.GameCodes[gameCode] = 0;
                return registration;
            });
    }

    internal ConnectionRegistration? Remove(string connectionId) =>
        connections.TryRemove(connectionId, out var registration) ? registration : null;

    public bool HasActiveConnection(Guid userId, string gameCode) =>
        connections.Values.Any(registration =>
            registration.UserId == userId && registration.GameCodes.ContainsKey(gameCode));
}

internal sealed record ConnectionRegistration(
    Guid UserId,
    ConcurrentDictionary<string, byte> GameCodes);
