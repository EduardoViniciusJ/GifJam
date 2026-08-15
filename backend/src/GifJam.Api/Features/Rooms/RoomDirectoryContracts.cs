using GifJam.Api.Domain.Enums;

namespace GifJam.Api.Features.Rooms;

public enum RoomDirectorySort
{
    Popular = 0,
    Recent = 1
}

public sealed record PublicRoomSummary(
    string Code,
    GameMode Mode,
    int TotalRounds,
    string HostDisplayName,
    string? HostAvatarUrl,
    int PlayerCount,
    int Capacity,
    DateTimeOffset CreatedAt);

public sealed record PublicRoomDirectoryResponse(
    IReadOnlyList<PublicRoomSummary> Items,
    int Page,
    int PageSize,
    int Total,
    DateTimeOffset ServerTime);
