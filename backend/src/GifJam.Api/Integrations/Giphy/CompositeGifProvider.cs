using System.Text.Json;
using GifJam.Api.Integrations.Klipy;
using Microsoft.AspNetCore.WebUtilities;

namespace GifJam.Api.Integrations.Giphy;

public sealed partial class CompositeGifProvider(
    KlipyGifProvider klipyProvider,
    GiphyGifProvider giphyProvider,
    ILogger<CompositeGifProvider> logger) : IGifProvider
{
    private const int MaximumGiphyOffset = 4999;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GifProviderSearchResult> SearchAsync(
        string query,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var state = DecodeCursor(cursor);
        var klipyTask = SearchKlipyAsync(klipyProvider, query, state.KlipyCursor, state.KlipyHasMore, cancellationToken);
        var giphyTask = SearchGiphyAsync(giphyProvider, query, state.GiphyOffset, state.GiphyHasMore, cancellationToken);
        await Task.WhenAll(klipyTask, giphyTask);

        var klipy = await klipyTask;
        var giphy = await giphyTask;
        if (klipy.Error is not null && giphy.Error is not null)
        {
            throw new GifProviderUnavailableException(
                "KLIPY and GIPHY were unavailable.",
                new AggregateException(klipy.Error, giphy.Error));
        }

        if (klipy.Error is not null)
        {
            LogPartialFailure(logger, "KLIPY", klipy.Error);
        }

        if (giphy.Error is not null)
        {
            LogPartialFailure(logger, "GIPHY", giphy.Error);
        }

        var items = Interleave(
            klipy.Result?.Items ?? [],
            giphy.Result?.Items ?? []);
        var nextCursor = EncodeCursor(
            klipy.Error is null ? klipy.Result?.NextCursor is not null : state.KlipyHasMore,
            klipy.Error is null ? klipy.Result?.NextCursor : state.KlipyCursor,
            giphy.Error is null ? ParseNextOffset(giphy.Result?.NextCursor) is not null : state.GiphyHasMore,
            giphy.Error is null ? ParseNextOffset(giphy.Result?.NextCursor) ?? 0 : state.GiphyOffset);

        return new(items, nextCursor);
    }

    private static async Task<ProviderOutcome> SearchKlipyAsync(
        KlipyGifProvider provider,
        string query,
        string? cursor,
        bool hasMore,
        CancellationToken cancellationToken)
    {
        if (!hasMore)
        {
            return ProviderOutcome.Empty;
        }

        try
        {
            return new(await provider.SearchAsync(query, cursor, cancellationToken), null);
        }
        catch (GifProviderUnavailableException exception)
        {
            return new(null, exception);
        }
    }

    private static async Task<ProviderOutcome> SearchGiphyAsync(
        GiphyGifProvider provider,
        string query,
        int giphyOffset,
        bool hasMore,
        CancellationToken cancellationToken)
    {
        if (!hasMore)
        {
            return ProviderOutcome.Empty;
        }

        try
        {
            return new(await provider.SearchAsync(query, giphyOffset, cancellationToken), null);
        }
        catch (GifProviderUnavailableException exception)
        {
            return new(null, exception);
        }
    }

    private static List<GifProviderItem> Interleave(
        IReadOnlyList<GifProviderItem> klipyItems,
        IReadOnlyList<GifProviderItem> giphyItems)
    {
        var items = new List<GifProviderItem>(klipyItems.Count + giphyItems.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxCount = Math.Max(klipyItems.Count, giphyItems.Count);
        for (var index = 0; index < maxCount; index++)
        {
            AddIfNew(klipyItems, index, items, seen);
            AddIfNew(giphyItems, index, items, seen);
        }

        return items;
    }

    private static void AddIfNew(
        IReadOnlyList<GifProviderItem> source,
        int index,
        List<GifProviderItem> destination,
        HashSet<string> seen)
    {
        if (index >= source.Count)
        {
            return;
        }

        var item = source[index];
        var key = string.IsNullOrWhiteSpace(item.MediaUrl)
            ? $"{item.Provider}:{item.ExternalId}"
            : item.MediaUrl;
        if (seen.Add(key))
        {
            destination.Add(item);
        }
    }

    private static CombinedCursor DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return new(true, null, true, 0);
        }

        try
        {
            var state = JsonSerializer.Deserialize<CombinedCursor>(
                WebEncoders.Base64UrlDecode(cursor),
                JsonOptions);
            if (state is null || state.GiphyOffset is < 0 or > MaximumGiphyOffset ||
                (!state.KlipyHasMore && !state.GiphyHasMore))
            {
                throw new GiphyCursorException("The GIF search cursor is invalid.");
            }

            return state;
        }
        catch (GiphyCursorException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new GiphyCursorException("The GIF search cursor is invalid.", exception);
        }
    }

    private static string? EncodeCursor(
        bool klipyHasMore,
        string? klipyCursor,
        bool giphyHasMore,
        int giphyOffset)
    {
        if (!klipyHasMore && !giphyHasMore)
        {
            return null;
        }

        var state = new CombinedCursor(klipyHasMore, klipyCursor, giphyHasMore, giphyOffset);
        return WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions));
    }

    private static int? ParseNextOffset(string? cursor) =>
        int.TryParse(cursor, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var offset) &&
        offset is >= 0 and <= MaximumGiphyOffset
            ? offset
            : null;

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "GIF provider {Provider} failed; continuing with the other provider")]
    private static partial void LogPartialFailure(ILogger logger, string provider, Exception exception);

    private sealed record CombinedCursor(
        bool KlipyHasMore,
        string? KlipyCursor,
        bool GiphyHasMore,
        int GiphyOffset);

    private sealed record ProviderOutcome(GifProviderSearchResult? Result, Exception? Error)
    {
        public static ProviderOutcome Empty { get; } = new(new([], null), null);
    }
}
