using GifJam.Api.Common.Auth;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Tests.Auth;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Gifs;

public sealed class GifSelectionTokenServiceTests
{
    private readonly TestClock clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void ValidTokenRoundTripsSignedMetadata()
    {
        var service = CreateService();
        var item = CreateItem();

        var token = service.Create("ABCDE", item);
        var payload = service.Validate(token, "ABCDE");

        Assert.Equal(item.ExternalId, payload.ExternalId);
        Assert.Equal(item.MediaUrl, payload.MediaUrl);
        Assert.Equal(clock.UtcNow.AddMinutes(2), payload.ExpiresAt);
    }

    [Fact]
    public void TamperedTokenIsRejected()
    {
        var service = CreateService();
        var token = service.Create("ABCDE", CreateItem());
        var tampered = $"{token[..^1]}{(token[^1] == 'A' ? 'B' : 'A')}";

        var exception = Assert.Throws<GifJam.Api.Common.Errors.ApiException>(() =>
            service.Validate(tampered, "ABCDE"));

        Assert.Equal("invalid_gif_selection", exception.Code);
    }

    [Fact]
    public void TokenForAnotherRoomIsRejected()
    {
        var service = CreateService();
        var token = service.Create("ABCDE", CreateItem());

        var exception = Assert.Throws<GifJam.Api.Common.Errors.ApiException>(() =>
            service.Validate(token, "FGHJK"));

        Assert.Equal("invalid_gif_selection", exception.Code);
    }

    [Fact]
    public void ExpiredTokenIsRejected()
    {
        var service = CreateService();
        var token = service.Create("ABCDE", CreateItem());
        clock.UtcNow = clock.UtcNow.AddMinutes(3);

        var exception = Assert.Throws<GifJam.Api.Common.Errors.ApiException>(() =>
            service.Validate(token, "ABCDE"));

        Assert.Equal("gif_selection_expired", exception.Code);
    }

    private GifSelectionTokenService CreateService() => new(
        Options.Create(new JwtOptions { SigningKey = new string('s', 64) }),
        clock);

    private static GifProviderItem CreateItem() => new(
        "gif-1",
        "A reaction",
        "https://static.klipy.test/preview.gif",
        "https://static.klipy.test/media.gif",
        480,
        270,
        240,
        135,
        "https://klipy.test/gifs/gif-1",
        "Powered by KLIPY");
}
