using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Domain.Entities;

public sealed class Round
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid GameId { get; set; }

    public int RoundNumber { get; set; }

    public RoundPhase Phase { get; set; }

    public Guid? SelectedPhraseId { get; set; }

    public DateTimeOffset PhaseEndsAt { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public Game Game { get; set; } = null!;

    public Phrase? SelectedPhrase { get; set; }

    public ICollection<Phrase> Phrases { get; } = [];

    public ICollection<PhraseVote> PhraseVotes { get; } = [];

    public ICollection<GifSubmission> GifSubmissions { get; } = [];

    public ICollection<GifVote> GifVotes { get; } = [];
}
