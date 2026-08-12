using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace GifJam.Api.Features.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";
    // The cookie intentionally has no __Host- prefix because local development
    // runs the Angular app over http://localhost. Production still uses Secure.
    public const string SessionCookieName = "gifjam-session";
    public const string CsrfCookieName = "gifjam-csrf";

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
                [FromBody] AuthExchangeRequest request,
                [FromServices] AuthService authService,
                HttpContext context,
                CancellationToken cancellationToken) =>
                {
                    var session = await authService.ExchangeAsync(request, cancellationToken);
                    AppendSessionCookie(context, session);
                    context.Response.Headers.CacheControl = "no-store";
                    return Results.Ok(new AuthResponse(session.ExpiresAt, session.User));
                })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("ExchangeAuthenticationCode");

        group.MapPost("/logout", (HttpContext context) =>
            {
                DeleteSessionCookie(context);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("Logout");

        group.MapDelete("/account", async (
                [FromBody] DeleteAccountRequest request,
                HttpContext context,
                [FromServices] AuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.DeleteAccountAsync(
                    context.User.GetRequiredUserId(),
                    request.Confirmation,
                    cancellationToken);
                DeleteSessionCookie(context);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("DeleteAccount");

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

    private static void AppendSessionCookie(HttpContext context, AuthSessionResult session)
    {
        context.Response.Cookies.Append(SessionCookieName, session.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = ShouldUseSecureCookies(context),
            SameSite = ShouldUseSecureCookies(context) ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            Expires = session.ExpiresAt
        });
    }

    private static void DeleteSessionCookie(HttpContext context)
    {
        var secure = ShouldUseSecureCookies(context);
        context.Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            Path = "/",
            Secure = secure,
            HttpOnly = true,
            SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax
        });
    }

    private static bool ShouldUseSecureCookies(HttpContext context) =>
        !context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Host.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}
