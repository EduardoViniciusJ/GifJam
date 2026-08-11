using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Domain.Entities;

public sealed class MatchmakingBatch
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public MatchmakingBatchStatus Status { get; set; } = MatchmakingBatchStatus.Waiting;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? DeadlineAt { get; set; }

    public DateTimeOffset? MatchedAt { get; set; }

    public Guid? GameId { get; set; }

    public Game? Game { get; set; }

    public ICollection<MatchmakingTicket> Tickets { get; } = [];
}
