using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Features.Rooms;

public sealed class RoomDirectoryService(AppDbContext dbContext, IClock clock)
{
    public async Task<PublicRoomDirectoryResponse> GetPublicAsync(
        string? sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000)
        {
            throw new BadRequestException("invalid_page", "Page must contain a value between 1 and 10000.");
        }

        if (pageSize is < 1 or > 50)
        {
            throw new BadRequestException("invalid_page_size", "Page size must contain between 1 and 50 items.");
        }

        var directorySort = ParseSort(sort);
        var query = dbContext.Games
            .AsNoTracking()
            .Where(game => game.Visibility == RoomVisibility.Public && game.Status == GameStatus.Lobby)
            .Select(game => new
            {
                game.Code,
                game.Mode,
                game.TotalRounds,
                game.HostUser.DisplayName,
                game.HostUser.AvatarUrl,
                game.CreatedAt,
                PlayerCount = game.Players.Count(player => player.LeftAt == null),
                HasConnectedPlayer = game.Players.Any(player =>
                    player.LeftAt == null && player.IsConnected)
            })
            .Where(room =>
                room.PlayerCount >= 1 &&
                room.PlayerCount < GameRules.MaximumPlayers &&
                room.HasConnectedPlayer);

        var total = await query.CountAsync(cancellationToken);
        var orderedQuery = directorySort == RoomDirectorySort.Recent
            ? query.OrderByDescending(room => room.CreatedAt).ThenBy(room => room.Code)
            : query.OrderByDescending(room => room.PlayerCount)
                .ThenByDescending(room => room.CreatedAt)
                .ThenBy(room => room.Code);
        var rooms = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(room => new PublicRoomSummary(
                room.Code,
                room.Mode,
                room.TotalRounds,
                room.DisplayName,
                room.AvatarUrl,
                room.PlayerCount,
                GameRules.MaximumPlayers,
                room.CreatedAt))
            .ToListAsync(cancellationToken);

        return new(rooms, page, pageSize, total, clock.UtcNow);
    }

    private static RoomDirectorySort ParseSort(string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort) ||
            string.Equals(sort, "popular", StringComparison.OrdinalIgnoreCase))
        {
            return RoomDirectorySort.Popular;
        }

        if (string.Equals(sort, "recent", StringComparison.OrdinalIgnoreCase))
        {
            return RoomDirectorySort.Recent;
        }

        throw new BadRequestException("invalid_room_sort", "Sort must be popular or recent.");
    }
}
