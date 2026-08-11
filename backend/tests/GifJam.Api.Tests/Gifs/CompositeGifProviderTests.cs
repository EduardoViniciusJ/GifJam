using System.Net;
using System.Text;
using GifJam.Api.Integrations.Giphy;
using GifJam.Api.Integrations.Klipy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Tests.Gifs;

public sealed class CompositeGifProviderTests
{
    [Fact]
    public async Task SearchInterleavesProvidersAndCarriesBothCursorsForward()
    {
        var klipyHandler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, """
            {
              "results": [
                {
                  "id": "klipy-1",
                  "title": "Klipy one",
                  "itemurl": "https://klipy.test/gifs/klipy-1",
                  "media_formats": {
                    "gif": { "url": "https://media.klipy.test/klipy-1.gif", "dims": [480, 270] },
                    "tinygif": { "url": "https://media.klipy.test/klipy-1-small.gif", "dims": [240, 135] }
                  }
                },
                {
                  "id": "klipy-2",
                  "title": "Klipy two",
                  "itemurl": "https://klipy.test/gifs/klipy-2",
                  "media_formats": {
                    "gif": { "url": "https://media.klipy.test/klipy-2.gif", "dims": [480, 270] },
                    "tinygif": { "url": "https://media.klipy.test/klipy-2-small.gif", "dims": [240, 135] }
                  }
                }
              ],
              "next": "klipy-next"
            }
            """));
        var giphyHandler = new StubHandler(_ => JsonResponse(HttpStatusCode.OK, """
            {
              "data": [
                {
                  "type": "gif",
                  "id": "giphy-1",
                  "url": "https://giphy.test/gifs/giphy-1",
                  "title": "Giphy one",
                  "images": {
                    "fixed_width": { "url": "https://media.giphy.test/giphy-1.gif", "width": "480", "height": "270" },
                    "fixed_width_small": { "url": "https://media.giphy.test/giphy-1-small.gif", "width": "240", "height": "135" }
                  }
                },
                {
                  "type": "gif",
                  "id": "giphy-2",
                  "url": "https://giphy.test/gifs/giphy-2",
                  "title": "Giphy two",
                  "images": {
                    "fixed_width": { "url": "https://media.giphy.test/giphy-2.gif", "width": "480", "height": "270" },
                    "fixed_width_small": { "url": "https://media.giphy.test/giphy-2-small.gif", "width": "240", "height": "135" }
                  }
                }
              ],
              "pagination": { "total_count": 50, "count": 2, "offset": 0 },
              "meta": { "status": 200 }
            }
            """));

        var klipy = new KlipyGifProvider(
            CreateClient(klipyHandler, "https://api.klipy.test"),
            Options.Create(new KlipyOptions { ApiKey = "klipy-test-key" }));
        var giphy = new GiphyGifProvider(
            CreateClient(giphyHandler, "https://api.giphy.test"),
            Options.Create(new GiphyOptions { ApiKey = "giphy-test-key" }));
        var composite = new CompositeGifProvider(
            klipy,
            giphy,
            NullLogger<CompositeGifProvider>.Instance);

        var result = await composite.SearchAsync("feliz", null, CancellationToken.None);

        Assert.Equal(["klipy-1", "giphy-1", "klipy-2", "giphy-2"],
            result.Items.Select(item => item.ExternalId));
        Assert.NotNull(result.NextCursor);

        await composite.SearchAsync("feliz", result.NextCursor, CancellationToken.None);

        Assert.Contains("pos=klipy-next", klipyHandler.LastRequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("offset=2", giphyHandler.LastRequestUri?.Query, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler, string baseAddress) => new(handler)
    {
        BaseAddress = new(baseAddress),
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(responseFactory(1));
        }
    }
}
