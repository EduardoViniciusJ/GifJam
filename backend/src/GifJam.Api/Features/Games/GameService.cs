using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.GameEngine;
using GifJam.Api.Realtime;
using Microsoft.EntityFrameworkCore;

namespace GifJam.Api.Features.Games;

public sealed class GameService(
    AppDbContext dbContext,
    IGameCodeGenerator codeGenerator,
    IGameLockManager lockManager,
    IGameRealtimeNotifier realtimeNotifier,
    GameStateProjector stateProjector,
    IClock clock,
    GameTelemetry gameTelemetry)
{
    private const int MaximumPlayers = 6;

    public async Task<PlayerGameSnapshot> CreateAsync(
        Guid userId,
        int totalRounds,
        CancellationToken cancellationToken)
    {
        if (totalRounds is < 3 or > 6)
        {
            throw new ApiException("invalid_round_count", "Total rounds must be between 3 and 6.", StatusCodes.Status400BadRequest);
        }

        var userExists = await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw UserNotFound();
        }

        var code = await GenerateUniqueCodeAsync(cancellationToken);
        var now = clock.UtcNow;
        var game = new Game
        {
            Code = code,
            HostUserId = userId,
            TotalRounds = totalRounds,
            CreatedAt = now
        };
        game.Players.Add(new()
        {
            GameId = game.Id,
            UserId = userId,
            IsReady = true,
            IsConnected = true,
            JoinedAt = now,
            LastSeenAt = now
        });

        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync(cancellationToken);
        gameTelemetry.GameCreated(game.Code, game.TotalRounds);
        return await GetAsync(code, userId, cancellationToken);
    }

    public async Task<PlayerGameSnapshot> JoinAsync(
        string code,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        var gameId = await FindGameIdAsync(normalizedCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        var existingPlayer = game.Players.SingleOrDefault(player => player.UserId == userId);

        if (existingPlayer is not null)
        {
            existingPlayer.IsConnected = true;
            existingPlayer.LastSeenAt = clock.UtcNow;
        }
        else
        {
            if (game.Status != GameStatus.Lobby)
            {
                throw new ApiException("game_already_started", "New players cannot join after the game starts.", StatusCodes.Status409Conflict);
            }

            if (game.Players.Count >= MaximumPlayers)
            {
                throw new ApiException("game_full", "The game already has six players.", StatusCodes.Status409Conflict);
            }

            if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            {
                throw UserNotFound();
            }

            var now = clock.UtcNow;
            game.Players.Add(new()
            {
                GameId = game.Id,
                UserId = userId,
                JoinedAt = now,
                LastSeenAt = now,
                IsConnected = true
            });
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await ReloadPlayersAsync(game, cancellationToken);
        var lobby = stateProjector.CreateLobbySnapshot(game);
        await realtimeNotifier.LobbyUpdatedAsync(game.Code, lobby, cancellationToken);
        return new(lobby, game.HostUserId == userId);
    }

    public async Task LeaveAsync(string code, Guid userId, CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(NormalizeCode(code), cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        var player = game.Players.SingleOrDefault(savedPlayer => savedPlayer.UserId == userId)
            ?? throw NotMember();

        if (game.Status == GameStatus.Lobby && game.HostUserId == userId)
        {
            game.Status = GameStatus.Closed;
            game.FinishedAt = clock.UtcNow;
            foreach (var lobbyPlayer in game.Players)
            {
                lobbyPlayer.IsConnected = false;
            }
        }
        else if (game.Status == GameStatus.Lobby)
        {
            game.Players.Remove(player);
            dbContext.GamePlayers.Remove(player);
        }
        else
        {
            player.IsConnected = false;
            player.LastSeenAt = clock.UtcNow;
        }

        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await ReloadPlayersAsync(game, cancellationToken);
        await realtimeNotifier.LobbyUpdatedAsync(game.Code, stateProjector.CreateLobbySnapshot(game), cancellationToken);
    }

    public async Task<PlayerGameSnapshot> GetAsync(
        string code,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(NormalizeCode(code), cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken, tracking: false);
        EnsureMembership(game, userId);
        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task<PlayerGameSnapshot> ConnectAsync(
        string code,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        var gameId = await FindGameIdAsync(normalizedCode, cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        var player = EnsureMembership(game, userId);
        player.IsConnected = true;
        player.LastSeenAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.PresenceChangedAsync(
            game.Code,
            stateProjector.CreatePresenceSnapshot(game),
            cancellationToken);
        return stateProjector.CreatePlayerSnapshot(game, userId);
    }

    public async Task DisconnectAsync(
        string code,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(code);
        var gameId = await dbContext.Games.AsNoTracking()
            .Where(game => game.Code == normalizedCode)
            .Select(game => (Guid?)game.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (gameId is null)
        {
            return;
        }

        await using var gameLock = await lockManager.AcquireAsync(gameId.Value, cancellationToken);
        var game = await LoadGameAsync(gameId.Value, cancellationToken);
        var player = game.Players.SingleOrDefault(savedPlayer => savedPlayer.UserId == userId);
        if (player is null || !player.IsConnected)
        {
            return;
        }

        player.IsConnected = false;
        player.LastSeenAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.PresenceChangedAsync(
            game.Code,
            stateProjector.CreatePresenceSnapshot(game),
            cancellationToken);
    }

    public async Task<LobbySnapshot> SetReadyAsync(
        string code,
        Guid userId,
        bool isReady,
        CancellationToken cancellationToken)
    {
        var gameId = await FindGameIdAsync(NormalizeCode(code), cancellationToken);
        await using var gameLock = await lockManager.AcquireAsync(gameId, cancellationToken);
        var game = await LoadGameAsync(gameId, cancellationToken);
        if (game.Status != GameStatus.Lobby)
        {
            throw new ApiException("game_not_in_lobby", "Ready status can only change in the lobby.", StatusCodes.Status409Conflict);
        }

        var player = EnsureMembership(game, userId);
        player.IsReady = game.HostUserId == userId || isReady;
        game.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        var lobby = stateProjector.CreateLobbySnapshot(game);
        await realtimeNotifier.LobbyUpdatedAsync(game.Code, lobby, cancellationToken);
        return lobby;
    }

    private async Task<Guid> FindGameIdAsync(string code, CancellationToken cancellationToken)
    {
        var gameId = await dbContext.Games.AsNoTracking()
            .Where(game => game.Code == code && game.Status != GameStatus.Closed)
            .Select(game => (Guid?)game.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return gameId ?? throw GameNotFound();
    }

    private async Task<Game> LoadGameAsync(
        Guid gameId,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        var query = dbContext.Games
            .Include(game => game.Players)
            .ThenInclude(player => player.User)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.Phrases)
            .ThenInclude(phrase => phrase.User)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.PhraseVotes)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.GifSubmissions)
            .ThenInclude(submission => submission.User)
            .Include(game => game.Rounds)
            .ThenInclude(round => round.GifVotes)
            .Where(game => game.Id == gameId);
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.AsSplitQuery().SingleAsync(cancellationToken);
    }

    private async Task ReloadPlayersAsync(Game game, CancellationToken cancellationToken)
    {
        await dbContext.Entry(game).Collection(savedGame => savedGame.Players).Query()
            .Include(player => player.User)
            .LoadAsync(cancellationToken);
    }

    private static GamePlayer EnsureMembership(Game game, Guid userId) =>
        game.Players.SingleOrDefault(player => player.UserId == userId) ?? throw NotMember();

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = codeGenerator.Generate();
            if (!await dbContext.Games.AnyAsync(game => game.Code == code, cancellationToken))
            {
                return code;
            }
        }

        throw new ApiException("game_code_unavailable", "A game code could not be generated.", StatusCodes.Status503ServiceUnavailable);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static ApiException GameNotFound() =>
        new("game_not_found", "The game was not found.", StatusCodes.Status404NotFound);

    private static ApiException NotMember() =>
        new("not_game_member", "You are not a member of this game.", StatusCodes.Status403Forbidden);

    private static ApiException UserNotFound() =>
        new("user_not_found", "The authenticated user was not found.", StatusCodes.Status401Unauthorized);
}
