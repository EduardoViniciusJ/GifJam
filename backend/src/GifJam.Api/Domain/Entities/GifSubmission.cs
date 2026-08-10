namespace GifJam.Api.Domain.Entities;

public sealed class GifSubmission
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RoundId { get; set; }

    public Guid UserId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string PreviewUrl { get; set; } = string.Empty;

    public string MediaUrl { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string Attribution { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int PreviewWidth { get; set; }

    public int PreviewHeight { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public Round Round { get; set; } = null!;

    public User User { get; set; } = null!;

    public ICollection<GifVote> Votes { get; } = [];
}
