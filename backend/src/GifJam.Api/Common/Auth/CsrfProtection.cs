using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using GifJam.Api.Features.Auth;

namespace GifJam.Api.Common.Auth;

public static class CsrfProtection
{
    private const string HeaderName = "X-CSRF-TOKEN";
    private const string ItemName = "GifJam.CsrfToken";

    public static IApplicationBuilder UseGifJamCsrf(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var csrfToken = context.Request.Cookies[AuthEndpoints.CsrfCookieName];
            if (string.IsNullOrWhiteSpace(csrfToken))
            {
                csrfToken = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
                var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
                context.Response.Cookies.Append(
                    AuthEndpoints.CsrfCookieName,
                    csrfToken,
                    AuthCookiePolicy.CreateCsrfCookie(environment));
            }

            context.Items[ItemName] = csrfToken;

            if (context.Request.Path.StartsWithSegments("/api") &&
                IsUnsafe(context.Request.Method) &&
                context.Request.Cookies.ContainsKey(AuthEndpoints.SessionCookieName))
            {
                var headerToken = context.Request.Headers[HeaderName].ToString();
                var cookieBytes = System.Text.Encoding.UTF8.GetBytes(csrfToken);
                var headerBytes = System.Text.Encoding.UTF8.GetBytes(headerToken);
                if (cookieBytes.Length == 0 || cookieBytes.Length != headerBytes.Length ||
                    !CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            await next(context);
        });
    }

    private static bool IsUnsafe(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    public static string GetToken(HttpContext context) =>
        context.Items[ItemName] as string
        ?? throw new InvalidOperationException("The CSRF middleware did not initialize a token.");
}
