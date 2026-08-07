using GifJam.Api.Common.Auth;

namespace GifJam.Api.Features.Gifs;

public static class GifEndpoints
{
    public const string SearchRateLimitPolicy = "gif-search";

    public static IEndpointRouteBuilder MapGifEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/games/{code}/gifs/search", async (
                string code,
                string q,
                string? cursor,
                HttpContext context,
                GifSearchService searchService,
                CancellationToken cancellationToken) =>
                Results.Ok(await searchService.SearchAsync(
                    code,
                    context.User.GetRequiredUserId(),
                    q,
                    cursor,
                    cancellationToken)))
            .RequireAuthorization()
            .RequireRateLimiting(SearchRateLimitPolicy)
            .Produces<GifSearchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithTags("GIFs")
            .WithName("SearchGifs");

        return endpoints;
    }
}
