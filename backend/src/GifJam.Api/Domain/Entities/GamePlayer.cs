namespace GifJam.Api.Domain.Entities;

public sealed class GamePlayer
{
    public Guid GameId { get; set; }

    public Guid UserId { get; set; }

    public int Score { get; set; }

    public int ResultReadyRoundNumber { get; set; }

    public bool IsReady { get; set; }

    public bool IsConnected { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? LeftAt { get; set; }

    public Game Game { get; set; } = null!;

    public User User { get; set; } = null!;
}
