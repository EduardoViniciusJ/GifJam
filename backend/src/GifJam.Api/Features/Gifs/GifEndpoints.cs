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
            .WithTags("GIFs")
            .WithName("SearchGifs");

        return endpoints;
    }
}
