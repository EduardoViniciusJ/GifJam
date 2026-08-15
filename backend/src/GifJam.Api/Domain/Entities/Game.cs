using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = string.Empty;

    public Guid HostUserId { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Lobby;

    public GameMode Mode { get; set; } = GameMode.Classic;

    public RoomVisibility Visibility { get; set; } = RoomVisibility.Private;

    public int TotalRounds { get; set; }

    public int PhraseSubmissionSeconds { get; set; } = 60;

    public int ResultsSeconds { get; set; } = 60;

    public int CurrentRoundNumber { get; set; }

    public long Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public User HostUser { get; set; } = null!;

    public ICollection<GamePlayer> Players { get; } = [];

    public ICollection<Round> Rounds { get; } = [];
}
