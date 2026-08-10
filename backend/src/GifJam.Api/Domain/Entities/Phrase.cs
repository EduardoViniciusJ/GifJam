namespace GifJam.Api.Domain.Entities;

public sealed class Phrase
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoundId { get; set; }

    public Guid UserId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; set; }

    public Round Round { get; set; } = null!;

    public User User { get; set; } = null!;

    public ICollection<PhraseVote> Votes { get; } = [];
}
