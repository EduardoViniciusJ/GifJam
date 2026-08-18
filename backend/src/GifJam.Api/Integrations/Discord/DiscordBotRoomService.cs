using GifJam.Api.Data;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Features.Games.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Integrations.Discord;

public sealed class DiscordBotRoomService(
    AppDbContext dbContext,
    DiscordIdentitySynchronizer identitySynchronizer,
    IGameService gameService)
{
    private const int DefaultTotalRounds = 3;
    private const int DefaultPhraseSubmissionSeconds = 60;
    private const int DefaultResultsSeconds = 60;

    public Task<DiscordBotRoomResult?> FindHostedLobbyAsync(
        string discordUserId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discordUserId);

        return dbContext.Games
            .AsNoTracking()
            .Where(game =>
                game.HostUser.DiscordId == discordUserId &&
                game.Status == GameStatus.Lobby)
            .OrderByDescending(game => game.CreatedAt)
            .Select(game => new DiscordBotRoomResult(
                game.Code,
                WasReused: true,
                game.Visibility,
                game.TotalRounds,
                game.PhraseSubmissionSeconds,
                game.ResultsSeconds,
                game.Mode))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> CloseHostedLobbyAsync(
        string discordUserId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discordUserId);

        var hostedRoom = await dbContext.Games
            .AsNoTracking()
            .Where(game =>
                game.HostUser.DiscordId == discordUserId &&
                game.Status == GameStatus.Lobby)
            .OrderByDescending(game => game.CreatedAt)
            .Select(game => new DiscordHostedRoom(game.Code, game.HostUserId))
            .FirstOrDefaultAsync(cancellationToken);
        if (hostedRoom is null)
        {
            return null;
        }

        await gameService.CloseAsync(hostedRoom.Code, hostedRoom.HostUserId, cancellationToken);
        return hostedRoom.Code;
    }

    public Task<DiscordBotRoomResult> CreateOrReuseAsync(
        DiscordIdentity identity,
        CancellationToken cancellationToken) =>
        identitySynchronizer.ExecuteAsUserAsync<DiscordBotRoomResult>(
            identity,
            async (user, operationCancellationToken) =>
            {
                var existingRoom = await dbContext.Games
                    .AsNoTracking()
                    .Where(game => game.HostUserId == user.Id && game.Status == GameStatus.Lobby)
                    .OrderByDescending(game => game.CreatedAt)
                    .Select(game => new DiscordBotRoomResult(
                        game.Code,
                        WasReused: true,
                        game.Visibility,
                        game.TotalRounds,
                        game.PhraseSubmissionSeconds,
                        game.ResultsSeconds,
                        game.Mode))
                    .FirstOrDefaultAsync(operationCancellationToken);

                if (existingRoom is not null)
                {
                    return existingRoom;
                }

                var created = await gameService.CreateAsync(
                    user.Id,
                    DefaultTotalRounds,
                    operationCancellationToken,
                    DefaultPhraseSubmissionSeconds,
                    DefaultResultsSeconds,
                    GameMode.Classic,
                    hostIsConnected: false);

                return new DiscordBotRoomResult(
                    created.Lobby.Code,
                    WasReused: false,
                    created.Lobby.Visibility,
                    created.Lobby.TotalRounds,
                    created.Lobby.PhraseSubmissionSeconds,
                    created.Lobby.ResultsSeconds,
                    created.Lobby.Mode);
            },
            cancellationToken);

    private sealed record DiscordHostedRoom(string Code, Guid HostUserId);
}

public sealed record DiscordBotRoomResult(
    string Code,
    bool WasReused,
    RoomVisibility Visibility,
    int TotalRounds,
    int PhraseSubmissionSeconds,
    int ResultsSeconds,
    GameMode Mode);
