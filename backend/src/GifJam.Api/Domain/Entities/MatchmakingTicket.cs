using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Domain.Entities;

public sealed class MatchmakingTicket
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid BatchId { get; set; }

    public Guid UserId { get; set; }

    public MatchmakingTicketStatus Status { get; set; } = MatchmakingTicketStatus.Waiting;

    public DateTimeOffset JoinedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public MatchmakingBatch Batch { get; set; } = null!;

    public User User { get; set; } = null!;
}
