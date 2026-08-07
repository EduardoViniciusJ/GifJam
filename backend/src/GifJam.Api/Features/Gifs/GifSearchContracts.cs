namespace GifJam.Api.Features.Gifs;

public sealed record GifSearchResponse(
    IReadOnlyList<GifSearchItem> Items,
    string? NextCursor,
    string SearchPlaceholder,
    string Attribution);

public sealed record GifSearchItem(
    string Id,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution,
    string SelectionToken);
