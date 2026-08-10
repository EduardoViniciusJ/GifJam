using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Features.Auth;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Auth;

public sealed class AuthStateServiceTests
{
    [Fact]
    public void StateSurvivesServiceRecreationAndExpires()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var options = Options.Create(new JwtOptions { SigningKey = new string('s', 64) });
        var firstInstance = new AuthStateService(options, clock);
        var state = firstInstance.Create("/game");

        var recreatedInstance = new AuthStateService(options, clock);
        Assert.Equal("/game", recreatedInstance.ReadReturnUrl(state));

        clock.UtcNow = clock.UtcNow.AddMinutes(6);
        var exception = Assert.Throws<ApiException>(() => recreatedInstance.ReadReturnUrl(state));
        Assert.Equal("invalid_oauth_state", exception.Code);
    }

    [Fact]
    public void TamperedStateIsRejected()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var options = Options.Create(new JwtOptions { SigningKey = new string('s', 64) });
        var service = new AuthStateService(options, clock);
        var state = service.Create("/");
        var tampered = $"{state[..^1]}{(state[^1] == 'A' ? 'B' : 'A')}";

        var exception = Assert.Throws<ApiException>(() => service.ReadReturnUrl(tampered));
        Assert.Equal("invalid_oauth_state", exception.Code);
    }
}
