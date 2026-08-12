using GifJam.Api.Common.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GifJam.Api.Tests.Auth;

public sealed class AuthCookiePolicyTests
{
    [Fact]
    public void DevelopmentUsesLoopbackCompatibleHttpOnlyCookies()
    {
        var environment = new TestHostEnvironment(Environments.Development);

        var session = AuthCookiePolicy.CreateSessionCookie(environment);
        var csrf = AuthCookiePolicy.CreateCsrfCookie(environment);

        Assert.True(session.HttpOnly);
        Assert.False(session.Secure);
        Assert.Equal(SameSiteMode.Lax, session.SameSite);
        Assert.True(csrf.HttpOnly);
    }

    [Fact]
    public void ProductionRequiresSecureCrossSiteCookies()
    {
        var environment = new TestHostEnvironment(Environments.Production);

        var session = AuthCookiePolicy.CreateSessionCookie(environment);
        var csrf = AuthCookiePolicy.CreateCsrfCookie(environment);

        Assert.True(session.HttpOnly);
        Assert.True(session.Secure);
        Assert.Equal(SameSiteMode.None, session.SameSite);
        Assert.True(csrf.HttpOnly);
        Assert.True(csrf.Secure);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "GifJam.Api.Tests";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
