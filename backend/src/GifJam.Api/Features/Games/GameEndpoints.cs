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
                    cancellationToken,
                    request.PhraseSubmissionSeconds,
                    request.ResultsSeconds,
                    request.Mode);
                return Results.Created($"/api/games/{snapshot.Lobby.Code}", snapshot);
            })
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<PlayerGameSnapshot>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
            .Produces<PlayerGameSnapshot>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("JoinGame");

        group.MapPut("/{code}/settings", async (
                string code,
                UpdateGameSettingsRequest request,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
                Results.Ok(await gameService.UpdateSettingsAsync(
                    code,
                    context.User.GetRequiredUserId(),
                    request.TotalRounds,
                    request.PhraseSubmissionSeconds,
                    request.ResultsSeconds,
                    cancellationToken,
                    request.Mode)))
            .RequireRateLimiting(WriteRateLimitPolicy)
            .Produces<LobbySnapshot>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateGameSettings");

        group.MapPost("/{code}/leave", async (
                string code,
                HttpContext context,
                GameService gameService,
                CancellationToken cancellationToken) =>
            {
                await gameService.LeaveAsync(code, context.User.GetRequiredUserId(), cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
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
            .Produces<PlayerGameSnapshot>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetGame");

        return endpoints;
    }
}
