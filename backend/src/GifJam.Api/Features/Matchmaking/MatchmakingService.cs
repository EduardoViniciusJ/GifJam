using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Domain.Rules;
using GifJam.Api.Features.Games.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Features.Matchmaking;

public sealed partial class MatchmakingService(
    AppDbContext dbContext,
    IGameService gameService,
    IMatchmakingQueueLock queueLock,
    IMatchmakingRealtimeNotifier realtimeNotifier,
    IClock clock,
    IOptions<MatchmakingOptions> options,
    ILogger<MatchmakingService> logger) : IMatchmakingService
{
    public async Task<MatchmakingSnapshot> JoinAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var lease = await queueLock.AcquireAsync(cancellationToken);
        await ProcessDueBatchAndNotifyAsync(cancellationToken);

        var currentTicket = await LoadActiveTicketAsync(userId, cancellationToken);
        if (currentTicket is not null)
        {
            return currentTicket.Status == MatchmakingTicketStatus.Waiting
                ? CreateWaitingSnapshot(currentTicket.Batch, clock.UtcNow)
                : CreateMatchedSnapshot(currentTicket.Batch, clock.UtcNow);
        }

        await LeaveActiveGamesAsync(userId, cancellationToken);

        var now = clock.UtcNow;
        var batch = await LoadWaitingBatchAsync(cancellationToken);
        if (batch is null)
        {
            batch = new MatchmakingBatch
            {
                CreatedAt = now
            };
            dbContext.MatchmakingBatches.Add(batch);
        }

        var ticket = new MatchmakingTicket
        {
            BatchId = batch.Id,
            UserId = userId,
            JoinedAt = now
        };
        batch.Tickets.Add(ticket);
        dbContext.MatchmakingTickets.Add(ticket);

        var waitingUserIds = GetWaitingUserIds(batch);
        if (waitingUserIds.Count == GameRules.MinimumPlayers && batch.DeadlineAt is null)
        {
            batch.DeadlineAt = now.AddSeconds(options.Value.BatchWindowSeconds);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (waitingUserIds.Count < GameRules.MaximumPlayers)
        {
            var snapshot = CreateWaitingSnapshot(batch, now);
            await NotifyWaitingPlayersAsync(waitingUserIds, snapshot, cancellationToken);
            return snapshot;
        }

        var match = await MatchBatchAsync(batch, now, cancellationToken);
        await NotifyMatchAsync(match, cancellationToken);
        return CreateMatchedSnapshot(match, now);
    }

    public async Task LeaveAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var lease = await queueLock.AcquireAsync(cancellationToken);
        var ticket = await dbContext.MatchmakingTickets
            .Include(savedTicket => savedTicket.Batch)
            .ThenInclude(batch => batch.Tickets)
            .Where(savedTicket => savedTicket.UserId == userId && savedTicket.Status == MatchmakingTicketStatus.Waiting)
            .SingleOrDefaultAsync(cancellationToken);
        if (ticket is null)
        {
            return;
        }

        ticket.Status = MatchmakingTicketStatus.Cancelled;
        ticket.CompletedAt = clock.UtcNow;
        if (GetWaitingTickets(ticket.Batch).Count == 0)
        {
            ticket.Batch.Status = MatchmakingBatchStatus.Closed;
        }
        else if (GetWaitingTickets(ticket.Batch).Count < GameRules.MinimumPlayers)
        {
            ticket.Batch.DeadlineAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var waitingUserIds = GetWaitingUserIds(ticket.Batch);
        if (waitingUserIds.Count > 0)
        {
            await NotifyWaitingPlayersAsync(
                waitingUserIds,
                CreateWaitingSnapshot(ticket.Batch, clock.UtcNow),
                cancellationToken);
        }
    }

    public async Task<MatchmakingSnapshot> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var lease = await queueLock.AcquireAsync(cancellationToken);
        await ProcessDueBatchAndNotifyAsync(cancellationToken);

        var ticket = await LoadActiveTicketAsync(userId, cancellationToken);
        if (ticket is null)
        {
            return CreateNotInQueueSnapshot(clock.UtcNow);
        }

        return ticket.Status == MatchmakingTicketStatus.Waiting
            ? CreateWaitingSnapshot(ticket.Batch, clock.UtcNow)
            : CreateMatchedSnapshot(ticket.Batch, clock.UtcNow);
    }

    public async Task ProcessDueBatchesAsync(CancellationToken cancellationToken)
    {
        await using var lease = await queueLock.AcquireAsync(cancellationToken);
        await ProcessDueBatchAndNotifyAsync(cancellationToken);
    }

    private async Task ProcessDueBatchAndNotifyAsync(CancellationToken cancellationToken)
    {
        var batch = await LoadWaitingBatchAsync(cancellationToken);
        if (batch?.DeadlineAt is null || batch.DeadlineAt > clock.UtcNow)
        {
            return;
        }

        var now = clock.UtcNow;
        var waitingTickets = GetWaitingTickets(batch);
        if (waitingTickets.Count < GameRules.MinimumPlayers)
        {
            batch.DeadlineAt = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var match = await MatchBatchAsync(batch, now, cancellationToken);
        await NotifyMatchAsync(match, cancellationToken);
    }

    private async Task LeaveActiveGamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeGameCodes = await dbContext.Games
            .AsNoTracking()
            .Where(game => game.Status == GameStatus.Lobby || game.Status == GameStatus.InProgress)
            .Where(game => game.Players.Any(player =>
                player.UserId == userId && player.LeftAt == null))
            .OrderBy(game => game.CreatedAt)
            .Select(game => game.Code)
            .ToListAsync(cancellationToken);

        foreach (var gameCode in activeGameCodes)
        {
            await gameService.LeaveAsync(gameCode, userId, cancellationToken);
        }
    }

    private async Task<MatchResult> MatchBatchAsync(
        MatchmakingBatch batch,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var waitingTickets = GetWaitingTickets(batch);
        var userIds = waitingTickets
            .OrderBy(ticket => ticket.JoinedAt)
            .ThenBy(ticket => ticket.Id)
            .Select(ticket => ticket.UserId)
            .ToArray();
        var hostUserId = userIds[0];

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var gameSnapshot = await gameService.CreateLobbyWithPlayersAsync(
            userIds,
            options.Value.DefaultTotalRounds,
            options.Value.DefaultPhraseSubmissionSeconds,
            options.Value.DefaultResultsSeconds,
            GameMode.Classic,
            cancellationToken);
        var gameId = await dbContext.Games
            .Where(game => game.Code == gameSnapshot.Lobby.Code)
            .Select(game => game.Id)
            .SingleAsync(cancellationToken);

        foreach (var ticket in waitingTickets)
        {
            ticket.Status = MatchmakingTicketStatus.Matched;
            ticket.CompletedAt = now;
        }

        batch.Status = MatchmakingBatchStatus.Matched;
        batch.MatchedAt = now;
        batch.GameId = gameId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            gameSnapshot.Lobby.Code,
            hostUserId,
            userIds,
            now);
    }

    private async Task<MatchmakingBatch?> LoadWaitingBatchAsync(CancellationToken cancellationToken) =>
        await dbContext.MatchmakingBatches
            .Include(batch => batch.Tickets.Where(ticket => ticket.Status == MatchmakingTicketStatus.Waiting))
            .Where(batch => batch.Status == MatchmakingBatchStatus.Waiting)
            .OrderBy(batch => batch.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<MatchmakingTicket?> LoadActiveTicketAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tickets = await dbContext.MatchmakingTickets
            .Include(ticket => ticket.Batch)
            .ThenInclude(batch => batch.Tickets)
            .Include(ticket => ticket.Batch)
            .ThenInclude(batch => batch.Game)
            .Where(ticket => ticket.UserId == userId &&
                (ticket.Status == MatchmakingTicketStatus.Waiting ||
                 (ticket.Status == MatchmakingTicketStatus.Matched &&
                  ticket.Batch.Game != null &&
                  (ticket.Batch.Game.Status == GameStatus.Lobby ||
                   ticket.Batch.Game.Status == GameStatus.InProgress) &&
                  ticket.Batch.Game.Players.Any(player =>
                      player.UserId == userId && player.LeftAt == null))))
            .OrderByDescending(ticket => ticket.JoinedAt)
            .ToListAsync(cancellationToken);

        return tickets.SingleOrDefault(ticket => ticket.Status == MatchmakingTicketStatus.Waiting)
            ?? tickets.FirstOrDefault(ticket => ticket.Status == MatchmakingTicketStatus.Matched);
    }

    private async Task NotifyMatchAsync(MatchResult match, CancellationToken cancellationToken)
    {
        var snapshot = new MatchFoundSnapshot(
            match.GameCode,
            match.HostUserId,
            match.UserIds.Count,
            match.MatchedAt);
        foreach (var userId in match.UserIds)
        {
            try
            {
                await realtimeNotifier.MatchFoundAsync(userId, snapshot, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogNotificationFailure(logger, userId, exception);
            }
        }
    }

    private async Task NotifyWaitingPlayersAsync(
        IReadOnlyList<Guid> userIds,
        MatchmakingSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            try
            {
                await realtimeNotifier.MatchmakingUpdatedAsync(userId, snapshot, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogNotificationFailure(logger, userId, exception);
            }
        }
    }

    private static MatchmakingSnapshot CreateWaitingSnapshot(MatchmakingBatch batch, DateTimeOffset now) =>
        new(
            MatchmakingStatus.Waiting,
            GetWaitingTickets(batch).Count,
            GameRules.MinimumPlayers,
            GameRules.MaximumPlayers,
            GetWaitingTickets(batch).OrderBy(ticket => ticket.JoinedAt).FirstOrDefault()?.UserId,
            batch.DeadlineAt,
            null,
            null,
            now);

    private static MatchmakingSnapshot CreateMatchedSnapshot(MatchResult match, DateTimeOffset now) =>
        new(
            MatchmakingStatus.Matched,
            match.UserIds.Count,
            GameRules.MinimumPlayers,
            GameRules.MaximumPlayers,
            match.HostUserId,
            null,
            match.GameCode,
            GameMode.Classic,
            now);

    private static MatchmakingSnapshot CreateMatchedSnapshot(MatchmakingBatch batch, DateTimeOffset now)
    {
        var game = batch.Game
            ?? throw new InvalidOperationException("A matched matchmaking batch must reference its game.");
        return new(
            MatchmakingStatus.Matched,
            batch.Tickets.Count(ticket => ticket.Status == MatchmakingTicketStatus.Matched),
            GameRules.MinimumPlayers,
            GameRules.MaximumPlayers,
            game.HostUserId,
            null,
            game.Code,
            game.Mode,
            now);
    }

    private static MatchmakingSnapshot CreateNotInQueueSnapshot(DateTimeOffset now) =>
        new(
            MatchmakingStatus.NotInQueue,
            0,
            GameRules.MinimumPlayers,
            GameRules.MaximumPlayers,
            null,
            null,
            null,
            null,
            now);

    private static List<MatchmakingTicket> GetWaitingTickets(MatchmakingBatch batch) =>
        batch.Tickets
            .Where(ticket => ticket.Status == MatchmakingTicketStatus.Waiting)
            .ToList();

    private static List<Guid> GetWaitingUserIds(MatchmakingBatch batch) =>
        GetWaitingTickets(batch)
            .OrderBy(ticket => ticket.JoinedAt)
            .Select(ticket => ticket.UserId)
            .ToList();

    [LoggerMessage(
        EventId = 4300,
        Level = LogLevel.Warning,
        Message = "Could not notify matchmaking update for user {UserId}")]
    private static partial void LogNotificationFailure(ILogger logger, Guid userId, Exception exception);

    private sealed record MatchResult(
        string GameCode,
        Guid HostUserId,
        IReadOnlyList<Guid> UserIds,
        DateTimeOffset MatchedAt);

}
