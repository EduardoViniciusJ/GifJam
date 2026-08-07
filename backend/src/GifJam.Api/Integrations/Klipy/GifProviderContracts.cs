namespace GifJam.Api.Integrations.Klipy;

public sealed record GifProviderSearchResult(
    IReadOnlyList<GifProviderItem> Items,
    string? NextCursor);

public sealed record GifProviderItem(
    string ExternalId,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution);

public interface IGifProvider
{
    Task<GifProviderSearchResult> SearchAsync(
        string query,
        string? cursor,
        CancellationToken cancellationToken);
}
