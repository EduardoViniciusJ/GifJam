using System.Net;
using System.Text;
using GifJam.Api.Integrations.Giphy;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Gifs;

public sealed class GiphyGifProviderTests
{
    [Fact]
    public async Task SearchNormalizesGifResponseAndUsesPaginationOffset()
    {
        const string json = """
            {
              "data": [{
                "type": "gif",
                "id": "giphy-42",
                "url": "https://giphy.test/gifs/giphy-42",
                "title": "Happy reaction",
                "images": {
                  "fixed_width": { "url": "https://media.giphy.test/giphy-42.gif", "width": "480", "height": "270" },
                  "fixed_width_small": { "url": "https://media.giphy.test/giphy-42-small.gif", "width": "200", "height": "112" }
                }
              }],
              "pagination": { "total_count": 50, "count": 24, "offset": 0 },
              "meta": { "status": 200, "msg": "OK" }
            }
            """;
        var handler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, json));
        var provider = CreateProvider(handler);

        var result = await provider.SearchAsync("feliz", 0, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("giphy-42", item.ExternalId);
        Assert.Equal("Happy reaction", item.Description);
        Assert.Equal("giphy", item.Provider);
        Assert.Equal(480, item.Width);
        Assert.Equal(200, item.PreviewWidth);
        Assert.Equal("24", result.NextCursor);
        Assert.Contains("api_key=test-giphy-key", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("limit=24", handler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("rating=g", handler.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchIgnoresNonGifItems()
    {
        const string json = """
            {
              "data": [{
                "type": "sticker",
                "id": "sticker-1",
                "url": "https://giphy.test/stickers/sticker-1",
                "images": {
                  "fixed_width": { "url": "https://media.giphy.test/sticker-1.gif", "width": "480", "height": "270" }
                }
              }],
              "pagination": { "total_count": 1, "count": 1, "offset": 0 },
              "meta": { "status": 200, "msg": "OK" }
            }
            """;
        var provider = CreateProvider(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, json)));

        var result = await provider.SearchAsync("teste", 0, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    private static GiphyGifProvider CreateProvider(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new("https://api.giphy.test"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        return new(client, Options.Create(new GiphyOptions
        {
            ApiKey = "test-giphy-key",
            BaseUrl = "https://api.giphy.test"
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
