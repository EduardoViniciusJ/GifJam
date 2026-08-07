namespace GifJam.Api.Domain.Entities;

public sealed class PhraseVote
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoundId { get; set; }

    public Guid PhraseId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Round Round { get; set; } = null!;

    public Phrase Phrase { get; set; } = null!;

    public User User { get; set; } = null!;
}
