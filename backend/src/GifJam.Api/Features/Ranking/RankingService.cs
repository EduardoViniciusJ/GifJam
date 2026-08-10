using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Features.Games;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Features.Ranking;

public sealed class RankingService(AppDbContext dbContext, IClock clock)
{
    private const int MaximumEntries = 100;

    public async Task<GlobalRankingSnapshot> GetGlobalAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.TotalScore > 0)
            .OrderByDescending(user => user.TotalScore)
            .ThenBy(user => user.DisplayName)
            .ThenBy(user => user.Id)
            .Take(MaximumEntries)
            .Select(user => new
            {
                user.Id,
                user.Username,
                user.DisplayName,
                user.AvatarUrl,
                user.TotalScore
            })
            .ToArrayAsync(cancellationToken);

        var entries = new List<GlobalRankingEntrySnapshot>(users.Length);
        for (var index = 0; index < users.Length; index++)
        {
            var user = users[index];
            var position = index > 0 && users[index - 1].TotalScore == user.TotalScore
                ? entries[index - 1].Position
                : index + 1;

            entries.Add(new(
                position,
                user.Id,
                user.Username,
                user.DisplayName,
                user.AvatarUrl,
                user.TotalScore,
                user.Id == currentUserId));
        }

        return new(entries, clock.UtcNow);
    }
}
