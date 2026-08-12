using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using GifJam.Api.Features.Auth;

namespace GifJam.Api.Common.Auth;

public static class CsrfProtection
{
    private const string HeaderName = "X-CSRF-TOKEN";

    public static IApplicationBuilder UseGifJamCsrf(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Cookies.ContainsKey(AuthEndpoints.CsrfCookieName))
            {
                context.Response.Cookies.Append(
                    AuthEndpoints.CsrfCookieName,
                    Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
                    new CookieOptions
                    {
                        HttpOnly = false,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        IsEssential = true
                    });
            }

            if (context.Request.Path.StartsWithSegments("/api") &&
                IsUnsafe(context.Request.Method) &&
                context.Request.Cookies.ContainsKey(AuthEndpoints.SessionCookieName))
            {
                var cookieToken = context.Request.Cookies[AuthEndpoints.CsrfCookieName];
                var headerToken = context.Request.Headers[HeaderName].ToString();
                var cookieBytes = System.Text.Encoding.UTF8.GetBytes(cookieToken ?? string.Empty);
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
}
