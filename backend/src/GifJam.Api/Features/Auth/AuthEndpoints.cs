using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;

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

                var safeReturnUrl = authService.ReadReturnUrl(state);
                try
                {
                    var result = await authService.CompleteDiscordLoginAsync(
                        code ?? string.Empty,
                        state,
                        cancellationToken);
                    return Results.Redirect(
                        authService.CreateFrontendCallbackUri(result.ReturnUrl, result.ExchangeCode).ToString());
                }
                catch (ApiException exception) when (IsDiscordFailure(exception.Code))
                {
                    return Results.Redirect(
                        authService.CreateFrontendCallbackUri(safeReturnUrl, error: exception.Code).ToString());
                }
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

    private static bool IsDiscordFailure(string code) => code is
        "discord_exchange_failed" or
        "discord_identity_failed" or
        "discord_invalid_response";
}
