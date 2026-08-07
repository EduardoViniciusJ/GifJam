namespace GifJam.Api.Domain.Entities;

public sealed class GifVote
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoundId { get; set; }

    public Guid GifSubmissionId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Round Round { get; set; } = null!;

    public GifSubmission GifSubmission { get; set; } = null!;

    public User User { get; set; } = null!;
}
