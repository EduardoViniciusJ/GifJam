using GifJam.Api.Common.Auth;
using GifJam.Api.Features.Games;

namespace GifJam.Api.Features.Ranking;

public static class RankingEndpoints
{
    public static IEndpointRouteBuilder MapRankingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/ranking", async (
                HttpContext context,
                RankingService rankingService,
                CancellationToken cancellationToken) =>
            Results.Ok(await rankingService.GetGlobalAsync(
                context.User.GetRequiredUserId(),
                cancellationToken)))
            .RequireAuthorization()
            .WithTags("Ranking")
            .Produces<GlobalRankingSnapshot>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetGlobalRanking");

        return endpoints;
    }
}
