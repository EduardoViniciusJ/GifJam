using GifJam.Api.Common.Auth;

namespace GifJam.Api.Features.Games;

public static class GameEndpoints
{
    public const string WriteRateLimitPolicy = "games-write";

    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/games")
            .RequireAuthorization()
            .WithTags("Games");

        group.MapPost("/", async (
                CreateGameRequest request,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
            {
                var snapshot = await gameService.CreateAsync(
                    context.User.GetRequiredUserId(),
                    request.TotalRounds,
                    cancellationToken);
                return Results.Created($"/api/games/{snapshot.Lobby.Code}", snapshot);
            })
            .RequireRateLimiting(WriteRateLimitPolicy)
            .WithName("CreateGame");

        group.MapPost("/{code}/join", async (
                string code,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
                Results.Ok(await gameService.JoinAsync(
                    code,
                    context.User.GetRequiredUserId(),
                    cancellationToken)))
            .RequireRateLimiting(WriteRateLimitPolicy)
            .WithName("JoinGame");

        group.MapPost("/{code}/leave", async (
                string code,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
            {
                await gameService.LeaveAsync(code, context.User.GetRequiredUserId(), cancellationToken);
                return Results.NoContent();
            })
            .WithName("LeaveGame");

        group.MapGet("/{code}", async (
                string code,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
                Results.Ok(await gameService.GetAsync(
                    code,
                    context.User.GetRequiredUserId(),
                    cancellationToken)))
            .WithName("GetGame");

        return endpoints;
    }
}
