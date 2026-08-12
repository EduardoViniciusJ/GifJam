namespace GifJam.Api.Common.Auth;

public static class AuthCookiePolicy
{
    public static CookieOptions CreateSessionCookie(
        IHostEnvironment environment,
        DateTimeOffset? expires = null) =>
        Create(environment, httpOnly: true, expires);

    public static CookieOptions CreateCsrfCookie(IHostEnvironment environment) =>
        Create(environment, httpOnly: true, expires: null);

    private static CookieOptions Create(
        IHostEnvironment environment,
        bool httpOnly,
        DateTimeOffset? expires)
    {
        var isProductionLike = !environment.IsDevelopment();
        return new()
        {
            HttpOnly = httpOnly,
            Secure = isProductionLike,
            SameSite = isProductionLike ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/",
            Expires = expires,
            IsEssential = true
        };
    }
}
