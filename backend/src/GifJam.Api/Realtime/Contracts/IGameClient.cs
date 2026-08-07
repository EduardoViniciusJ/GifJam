using GifJam.Api.Features.Games;

namespace GifJam.Api.Realtime.Contracts;

public interface IGameClient
{
    Task StateSynced(PlayerGameSnapshot snapshot);

    Task LobbyUpdated(LobbySnapshot snapshot);

    Task PresenceChanged(PresenceSnapshot snapshot);

    Task PhaseChanged(RoundPhaseSnapshot snapshot);

    Task SubmissionProgress(SubmissionProgressSnapshot progress);

    Task CommandRejected(CommandRejectedMessage rejection);
}

public sealed record CommandRejectedMessage(string Code, string Message, string? CurrentPhase = null);
