using GifJam.Api.Common.Errors;
using GifJam.Api.Data;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Integrations.Klipy;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Features.Gifs;

public sealed partial class GifSearchService(
    AppDbContext dbContext,
    IGifProvider gifProvider,
    GifSelectionTokenService tokenService,
    ILogger<GifSearchService> logger)
{
    public async Task<GifSearchResponse> SearchAsync(
        string gameCode,
        Guid userId,
        string query,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var normalizedCode = gameCode.Trim().ToUpperInvariant();
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length is < 1 or > 80)
        {
            throw new ApiException(
                "invalid_gif_query",
                "The GIF search query must contain between 1 and 80 characters.",
                StatusCodes.Status400BadRequest);
        }

        if (cursor?.Length > 256)
        {
            throw new ApiException(
                "invalid_gif_cursor",
                "The GIF search cursor is invalid.",
                StatusCodes.Status400BadRequest);
        }

        var game = await dbContext.Games.AsNoTracking()
            .Include(savedGame => savedGame.Players)
            .Include(savedGame => savedGame.Rounds)
            .SingleOrDefaultAsync(
                savedGame => savedGame.Code == normalizedCode && savedGame.Status != GameStatus.Closed,
                cancellationToken)
            ?? throw new ApiException("game_not_found", "The game was not found.", StatusCodes.Status404NotFound);
        if (game.Players.All(player => player.UserId != userId))
        {
            throw new ApiException("not_game_member", "You are not a member of this game.", StatusCodes.Status403Forbidden);
        }

        var round = game.Rounds.SingleOrDefault(savedRound => savedRound.RoundNumber == game.CurrentRoundNumber)
            ?? throw new ApiException("round_not_found", "The current round was not found.", StatusCodes.Status409Conflict);
        if (round.Phase != RoundPhase.GifSubmission)
        {
            throw new ApiException(
                "invalid_round_phase",
                "GIF search is only available during GifSubmission.",
                StatusCodes.Status409Conflict);
        }

        try
        {
            var result = await gifProvider.SearchAsync(normalizedQuery, cursor, cancellationToken);
            var items = result.Items.Select(item => new GifSearchItem(
                item.ExternalId,
                item.Description,
                item.PreviewUrl,
                item.MediaUrl,
                item.Width,
                item.Height,
                item.PreviewWidth,
                item.PreviewHeight,
                item.SourceUrl,
                item.Attribution,
                tokenService.Create(normalizedCode, item))).ToArray();
            return new(items, result.NextCursor, "Search KLIPY", "Powered by KLIPY");
        }
        catch (GifProviderUnavailableException exception)
        {
            LogProviderUnavailable(logger, exception);
            throw new ApiException(
                "gif_provider_unavailable",
                "GIF search is temporarily unavailable. Try again shortly.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    [LoggerMessage(EventId = 5000, Level = LogLevel.Warning, Message = "GIF provider request failed")]
    private static partial void LogProviderUnavailable(ILogger logger, Exception exception);
}
