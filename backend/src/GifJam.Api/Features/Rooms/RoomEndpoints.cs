namespace GifJam.Api.Features.Rooms;

public static class RoomEndpoints
{
    public const string ReadRateLimitPolicy = "rooms-read";

    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/rooms/public", async (
                string? sort,
                int? page,
                int? pageSize,
                RoomDirectoryService directoryService,
                CancellationToken cancellationToken) =>
                Results.Ok(await directoryService.GetPublicAsync(
                    sort,
                    page ?? 1,
                    pageSize ?? 20,
                    cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting(ReadRateLimitPolicy)
            .Produces<PublicRoomDirectoryResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithTags("Rooms")
            .WithName("GetPublicRooms");

        return endpoints;
    }
}
