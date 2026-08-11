using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Integrations.Klipy;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Integrations.Giphy;

public interface IGiphyGifProvider
{
    Task<GifProviderSearchResult> SearchAsync(
        string query,
        int offset,
        CancellationToken cancellationToken);
}

public sealed class GiphyGifProvider(
    HttpClient httpClient,
    IOptions<GiphyOptions> options) : IGiphyGifProvider
{
    private const int MaximumResults = 24;
    private const int MaximumAttempts = 3;
    private readonly GiphyOptions settings = options.Value;

    public async Task<GifProviderSearchResult> SearchAsync(
        string query,
        int offset,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["api_key"] = settings.ApiKey,
            ["q"] = query,
            ["limit"] = MaximumResults.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["offset"] = offset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["rating"] = settings.Rating,
            ["lang"] = settings.Language,
            ["bundle"] = settings.Bundle
        };
        var requestUri = QueryHelpers.AddQueryString("/v1/gifs/search", parameters);

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
                    var payload = await response.Content.ReadFromJsonAsync<GiphyResponse>(cancellationToken)
                        ?? throw new GifProviderUnavailableException("GIPHY returned an empty response.");
                    if (payload.Meta?.Status != StatusCodes.Status200OK)
                    {
                        throw new GifProviderUnavailableException("GIPHY returned an invalid response.");
                    }

                    return Normalize(payload, offset);
                }

                if (!IsTransient(response.StatusCode) || attempt == MaximumAttempts)
                {
                    throw new GifProviderUnavailableException(
                        $"GIPHY returned HTTP {(int)response.StatusCode}.");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < MaximumAttempts)
            {
                // HttpClient timeout is transient; the caller's cancellation is never retried.
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new GifProviderUnavailableException("GIPHY timed out.", exception);
            }
            catch (HttpRequestException) when (attempt < MaximumAttempts)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
                continue;
            }
            catch (HttpRequestException exception)
            {
                throw new GifProviderUnavailableException("GIPHY could not be reached.", exception);
            }

            await DelayBeforeRetryAsync(attempt, cancellationToken);
        }

        throw new GifProviderUnavailableException("GIPHY could not be reached.");
    }

    private static GifProviderSearchResult Normalize(GiphyResponse payload, int offset)
    {
        var items = payload.Data
            .Select(NormalizeItem)
            .Where(item => item is not null)
            .Take(MaximumResults)
            .Cast<GifProviderItem>()
            .ToArray();

        var fetchedCount = payload.Pagination?.Count ?? items.Length;
        var totalCount = payload.Pagination?.TotalCount;
        var hasMore = fetchedCount > 0 && (totalCount is not null
            ? offset + fetchedCount < totalCount.Value
            : fetchedCount >= MaximumResults);
        var nextCursor = hasMore
            ? (offset + fetchedCount).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return new(items, nextCursor);
    }

    private static GifProviderItem? NormalizeItem(GiphyItem item)
    {
        if (!string.Equals(item.Type, "gif", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        var media = FirstImage(item.Images.FixedWidth, item.Images.DownsizedMedium, item.Images.Downsized, item.Images.Original);
        var preview = FirstImage(item.Images.FixedWidthSmall, item.Images.PreviewGif, item.Images.FixedWidth, media);
        if (media is null || preview is null ||
            !IsSafeHttpsUrl(media.Url) || !IsSafeHttpsUrl(preview.Url) ||
            !IsSafeHttpsUrl(item.Url))
        {
            return null;
        }

        return new(
            item.Id,
            item.Title,
            preview.Url,
            media.Url,
            ParseDimension(media.Width),
            ParseDimension(media.Height),
            ParseDimension(preview.Width),
            ParseDimension(preview.Height),
            item.Url,
            "Powered by GIPHY",
            "giphy");
    }

    private static GiphyImage? FirstImage(params GiphyImage?[] images) =>
        images.FirstOrDefault(image => image is not null && !string.IsNullOrWhiteSpace(image.Url));

    private static int ParseDimension(string value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var dimension) && dimension > 0
            ? dimension
            : 1;

    private static bool IsSafeHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)statusCode >= StatusCodes.Status500InternalServerError;

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken);

    private sealed class GiphyResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyList<GiphyItem> Data { get; init; } = [];

        [JsonPropertyName("pagination")]
        public GiphyPagination? Pagination { get; init; }

        [JsonPropertyName("meta")]
        public GiphyMeta? Meta { get; init; }
    }

    private sealed class GiphyItem
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("images")]
        public GiphyImageSet Images { get; init; } = new();
    }

    private sealed class GiphyImageSet
    {
        [JsonPropertyName("fixed_width")]
        public GiphyImage? FixedWidth { get; init; }

        [JsonPropertyName("fixed_width_small")]
        public GiphyImage? FixedWidthSmall { get; init; }

        [JsonPropertyName("downsized_medium")]
        public GiphyImage? DownsizedMedium { get; init; }

        [JsonPropertyName("downsized")]
        public GiphyImage? Downsized { get; init; }

        [JsonPropertyName("preview_gif")]
        public GiphyImage? PreviewGif { get; init; }

        [JsonPropertyName("original")]
        public GiphyImage? Original { get; init; }
    }

    private sealed class GiphyImage
    {
        [JsonPropertyName("url")]
        public string Url { get; init; } = string.Empty;

        [JsonPropertyName("width")]
        public string Width { get; init; } = string.Empty;

        [JsonPropertyName("height")]
        public string Height { get; init; } = string.Empty;
    }

    private sealed class GiphyPagination
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; init; }

        [JsonPropertyName("count")]
        public int Count { get; init; }

        [JsonPropertyName("offset")]
        public int Offset { get; init; }
    }

    private sealed class GiphyMeta
    {
        [JsonPropertyName("status")]
        public int Status { get; init; }
    }
}
