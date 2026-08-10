using System.Net;
using System.Text;
using GifJam.Api.Integrations.Klipy;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Gifs;

public sealed class KlipyGifProviderTests
{
    [Fact]
    public async Task SearchNormalizesKlipyResponseAndCursor()
    {
        const string json = """
            {
              "results": [{
                "id": "gif-42",
                "title": "Reaction",
                "content_description": "Happy reaction",
                "itemurl": "https://klipy.test/gifs/gif-42",
                "media_formats": {
                  "gif": { "url": "https://static.klipy.test/media.gif", "dims": [480, 270] },
                  "tinygif": { "url": "https://static.klipy.test/preview.gif", "dims": [240, 135] }
                }
              }],
              "next": "next-page"
            }
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, json));
        var provider = CreateProvider(handler);

        var result = await provider.SearchAsync("feliz", null, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("gif-42", item.ExternalId);
        Assert.Equal("Happy reaction", item.Description);
        Assert.Equal(480, item.Width);
        Assert.Equal(240, item.PreviewWidth);
        Assert.Equal("next-page", result.NextCursor);
        Assert.Contains("locale=pt_BR", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("contentfilter=high", handler.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchRetriesOnlyTransientResponses()
    {
        var handler = new StubHandler(call => call < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : JsonResponse(HttpStatusCode.OK, "{\"results\":[],\"next\":null}"));
        var provider = CreateProvider(handler);

        await provider.SearchAsync("teste", null, CancellationToken.None);

        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SearchDoesNotRetryClientErrors()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<GifProviderUnavailableException>(() =>
            provider.SearchAsync("teste", null, CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
    }

    private static KlipyGifProvider CreateProvider(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new("https://api.klipy.test"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        return new(client, Options.Create(new KlipyOptions
        {
            ApiKey = "secret-test-key",
            BaseUrl = "https://api.klipy.test"
        }));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(CallCount));
        }
    }
}
