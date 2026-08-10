using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Integrations.Klipy;

public sealed class KlipyGifProvider(
    HttpClient httpClient,
    IOptions<KlipyOptions> options) : IGifProvider
{
    private const int MaximumResults = 24;
    private const int MaximumAttempts = 3;
    private readonly KlipyOptions options = options.Value;

    public async Task<GifProviderSearchResult> SearchAsync(
        string query,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["q"] = query,
            ["key"] = options.ApiKey,
            ["limit"] = MaximumResults.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["locale"] = options.Locale,
            ["country"] = options.Country,
            ["contentfilter"] = options.ContentFilter,
            ["media_filter"] = "gif,tinygif,mediumgif,nanogif,preview",
            ["pos"] = cursor
        };
        var requestUri = QueryHelpers.AddQueryString("/v2/search", parameters);

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(
                    requestUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<KlipyResponse>(cancellationToken)
                        ?? throw new GifProviderUnavailableException("KLIPY returned an empty response.");
                    return Normalize(payload);
                }

                if (!IsTransient(response.StatusCode) || attempt == MaximumAttempts)
                {
                    throw new GifProviderUnavailableException(
                        $"KLIPY returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaximumAttempts)
            {
                // HttpClient timeout is transient; the caller's cancellation is never retried.
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new GifProviderUnavailableException("KLIPY timed out.", exception);
            }
            catch (HttpRequestException) when (attempt < MaximumAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
                continue;
            }
            catch (HttpRequestException exception)
            {
                throw new GifProviderUnavailableException("KLIPY could not be reached.", exception);
            }

            await DelayBeforeRetryAsync(attempt, cancellationToken);
        }

        throw new GifProviderUnavailableException("KLIPY could not be reached.");
    }

    private static GifProviderSearchResult Normalize(KlipyResponse payload)
    {
        var items = payload.Results
            .Select(NormalizeItem)
            .Where(item => item is not null)
            .Take(MaximumResults)
            .Cast<GifProviderItem>()
            .ToArray();
        return new(items, string.IsNullOrWhiteSpace(payload.Next) ? null : payload.Next);
    }

    private static GifProviderItem? NormalizeItem(KlipyItem item)
    {
        var media = SelectFormat(item.MediaFormats, "gif", "mediumgif", "tinygif", "nanogif");
        var preview = SelectFormat(item.MediaFormats, "tinygif", "nanogif", "preview") ?? media;
        if (media is null || preview is null ||
            !IsSafeHttpsUrl(media.Url) || !IsSafeHttpsUrl(preview.Url) ||
            !IsSafeHttpsUrl(item.ItemUrl))
        {
            return null;
        }

        return new(
            item.Id,
            string.IsNullOrWhiteSpace(item.ContentDescription) ? item.Title : item.ContentDescription,
            preview.Url,
            media.Url,
            GetDimension(media.Dimensions, 0),
            GetDimension(media.Dimensions, 1),
            GetDimension(preview.Dimensions, 0),
            GetDimension(preview.Dimensions, 1),
            item.ItemUrl,
            "Powered by KLIPY");
    }

    private static KlipyMediaFormat? SelectFormat(
        IReadOnlyDictionary<string, KlipyMediaFormat> formats,
        params string[] preference) =>
        preference.Select(name => formats.GetValueOrDefault(name)).FirstOrDefault(format => format is not null);

    private static int GetDimension(IReadOnlyList<int> dimensions, int index) =>
        dimensions.Count > index && dimensions[index] > 0 ? dimensions[index] : 1;

    private static bool IsSafeHttpsUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= StatusCodes.Status500InternalServerError;

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);

    private sealed class KlipyResponse
    {
        [JsonPropertyName("results")]
        public IReadOnlyList<KlipyItem> Results { get; init; } = [];

        [JsonPropertyName("next")]
        public string? Next { get; init; }
    }

    private sealed class KlipyItem
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("content_description")]
        public string ContentDescription { get; init; } = string.Empty;

        [JsonPropertyName("itemurl")]
        public string ItemUrl { get; init; } = string.Empty;

        [JsonPropertyName("media_formats")]
        public IReadOnlyDictionary<string, KlipyMediaFormat> MediaFormats { get; init; } =
            new Dictionary<string, KlipyMediaFormat>(StringComparer.Ordinal);
    }

    private sealed class KlipyMediaFormat
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("dims")]
        public IReadOnlyList<int> Dimensions { get; init; } = [];
    }
}
