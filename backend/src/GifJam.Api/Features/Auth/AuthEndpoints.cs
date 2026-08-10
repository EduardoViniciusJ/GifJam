using GifJam.Api.Common.Auth;

namespace GifJam.Api.Features.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/discord/start", (string? returnUrl, AuthService authService) =>
                Results.Redirect(authService.CreateAuthorizationUri(returnUrl).ToString()))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .Produces(StatusCodes.Status302Found)
            .WithName("StartDiscordAuthentication");

        group.MapGet("/discord/callback", async (
                string? code,
                string? state,
                string? error,
                AuthService authService,
                CancellationToken cancellationToken) =>
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    var returnUrl = authService.ReadReturnUrl(state);
                    return Results.Redirect(authService.CreateFrontendCallbackUri(returnUrl, error: "access_denied").ToString());
                }

                var result = await authService.CompleteDiscordLoginAsync(code ?? string.Empty, state, cancellationToken);
                return Results.Redirect(authService.CreateFrontendCallbackUri(result.ReturnUrl, result.ExchangeCode).ToString());
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("CompleteDiscordAuthentication");

        group.MapPost("/exchange", async (
                AuthExchangeRequest request,
                AuthService authService,
                CancellationToken cancellationToken) =>
                Results.Ok(await authService.ExchangeAsync(request, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("ExchangeAuthenticationCode");

        group.MapGet("/me", async (
                HttpContext context,
                AuthService authService,
                CancellationToken cancellationToken) =>
                Results.Ok(await authService.GetCurrentUserAsync(
                    context.User.GetRequiredUserId(),
                    cancellationToken)))
            .RequireAuthorization()
            .Produces<AuthUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("GetCurrentUser");

        return endpoints;
    }
}
