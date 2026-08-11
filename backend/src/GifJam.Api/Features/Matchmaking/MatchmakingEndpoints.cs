using GifJam.Api.Common.Auth;

namespace GifJam.Api.Features.Matchmaking;

public static class MatchmakingEndpoints
{
    public const string WriteRateLimitPolicy = "matchmaking-write";

    public static IEndpointRouteBuilder MapMatchmakingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/matchmaking")
            .RequireAuthorization()
            .WithTags("Matchmaking");

        group.MapPost("/join", async (
                HttpContext context,
                IMatchmakingService matchmakingService,
                CancellationToken cancellationToken) =>
                Results.Ok(await matchmakingService.JoinAsync(
                    context.User.GetRequiredUserId(),
                    cancellationToken)))
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<MatchmakingSnapshot>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("JoinMatchmaking");

        group.MapPost("/leave", async (
                HttpContext context,
                IMatchmakingService matchmakingService,
                CancellationToken cancellationToken) =>
            {
                await matchmakingService.LeaveAsync(
                    context.User.GetRequiredUserId(),
                    cancellationToken);
                return Results.NoContent();
            })
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("LeaveMatchmaking");

        group.MapGet("/status", async (
                HttpContext context,
                IMatchmakingService matchmakingService,
                CancellationToken cancellationToken) =>
                Results.Ok(await matchmakingService.GetStatusAsync(
                    context.User.GetRequiredUserId(),
                    cancellationToken)))
            .Produces<MatchmakingSnapshot>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetMatchmakingStatus");

        return endpoints;
    }
}
