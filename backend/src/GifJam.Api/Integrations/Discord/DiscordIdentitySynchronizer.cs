using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Integrations.Discord;

public sealed class DiscordIdentitySynchronizer(
    AppDbContext dbContext,
    IDiscordUserLockManager lockManager,
    IClock clock)
{
    public async Task<TResult> ExecuteAsUserAsync<TResult>(
        DiscordIdentity identity,
        Func<User, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.DiscordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Username);

        await using var userLock = await lockManager.AcquireAsync(identity.DiscordId, cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            savedUser => savedUser.DiscordId == identity.DiscordId,
            cancellationToken);
        var now = clock.UtcNow;

        if (user is null)
        {
            user = new()
            {
                DiscordId = identity.DiscordId,
                CreatedAt = now
            };
            dbContext.Users.Add(user);
        }

        user.Username = identity.Username;
        user.DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName)
            ? identity.Username
            : identity.DisplayName;
        user.AvatarUrl = identity.AvatarUrl;
        user.UpdatedAt = now;

        // Persist before the callback so services that verify the user through a
        // database query can immediately use newly provisioned Discord accounts.
        await dbContext.SaveChangesAsync(cancellationToken);
        return await operation(user, cancellationToken);
    }
}
