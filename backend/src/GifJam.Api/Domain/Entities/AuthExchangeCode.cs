namespace GifJam.Api.Domain.Entities;

public sealed class AuthExchangeCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string CodeHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public User User { get; set; } = null!;
}
