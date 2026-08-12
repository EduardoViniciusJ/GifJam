using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Features.Games;
using GifJam.Api.Features.Games.Interfaces;
using GifJam.Api.Features.Games.Services;
using GifJam.Api.GameEngine;
using GifJam.Api.Domain.Enums;
using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

[Authorize]
public sealed partial class GameHub(
    IGameService gameService,
    GameCoordinator gameCoordinator,
    GameConnectionRegistry connectionRegistry,
    SignalRCommandRateLimiter rateLimiter,
    ILogger<GameHub> logger) : Hub<IGameClient>
{
    public async Task SubscribeGame(string gameCode)
    {
        await ExecuteCommandAsync(async () =>
        {
            GameHubInputValidator.ValidateGameCode(gameCode);
            var userId = Context.User!.GetRequiredUserId();
            var snapshot = await gameService.ConnectAsync(gameCode, userId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                GameGroups.ForCode(snapshot.Lobby.Code),
                Context.ConnectionAborted);
            connectionRegistry.Track(Context.ConnectionId, userId, snapshot.Lobby.Code);
            await Clients.Caller.StateSynced(snapshot);
        });
    }

    public Task SetReady(string gameCode, bool isReady) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameService.SetReadyAsync(gameCode, userId, isReady, Context.ConnectionAborted);
    });

    public Task UpdateGameSettings(
        string gameCode,
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds) => UpdateGameSettingsCore(
        gameCode,
        totalRounds,
        phraseSubmissionSeconds,
        resultsSeconds,
        GameMode.Classic);

    public Task UpdateGameSettingsWithMode(
        string gameCode,
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds,
        GameMode mode) => UpdateGameSettingsCore(
        gameCode,
        totalRounds,
        phraseSubmissionSeconds,
        resultsSeconds,
        mode);

    private Task UpdateGameSettingsCore(
        string gameCode,
        int totalRounds,
        int phraseSubmissionSeconds,
        int resultsSeconds,
        GameMode mode) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameService.UpdateSettingsAsync(
            gameCode,
            userId,
            totalRounds,
            phraseSubmissionSeconds,
            resultsSeconds,
            Context.ConnectionAborted,
            mode);
    });

    public Task RequestSync(string gameCode) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        // Validate membership before allowing a sync-triggered transition.
        await gameService.GetAsync(gameCode, userId, Context.ConnectionAborted);
        // Recover this game's expired round before returning its snapshot. The
        // operation is scoped to the requested game, so sync never performs a
        // global scan and still works if the background scheduler is delayed.
        await gameCoordinator.ProcessExpiredRoundAsync(gameCode, Context.ConnectionAborted);
        var snapshot = await gameService.GetAsync(gameCode, userId, Context.ConnectionAborted);
        await Clients.Caller.StateSynced(snapshot);
    });

    public Task StartGame(string gameCode) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.StartGameAsync(gameCode, userId, Context.ConnectionAborted);
    });

    public Task SubmitPhrase(string gameCode, string text) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        GameHubInputValidator.ValidatePhrase(text);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.SubmitPhraseAsync(gameCode, userId, text, Context.ConnectionAborted);
    });

    public Task VotePhrase(string gameCode, Guid phraseId) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.VotePhraseAsync(gameCode, userId, phraseId, Context.ConnectionAborted);
    });

    public Task SubmitGif(string gameCode, string selectionToken) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        GameHubInputValidator.ValidateSelectionToken(selectionToken);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.SubmitGifAsync(gameCode, userId, selectionToken, Context.ConnectionAborted);
    });

    public Task VoteGif(string gameCode, Guid gifSubmissionId) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.VoteGifAsync(gameCode, userId, gifSubmissionId, Context.ConnectionAborted);
    });

    public Task SetResultsReady(string gameCode) => ExecuteCommandAsync(async () =>
    {
        GameHubInputValidator.ValidateGameCode(gameCode);
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.SetResultsReadyAsync(gameCode, userId, Context.ConnectionAborted);
    });

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var registration = connectionRegistry.Remove(Context.ConnectionId);
        if (registration is not null)
        {
            foreach (var gameCode in registration.GameCodes.Keys)
            {
                if (!connectionRegistry.HasActiveConnection(registration.UserId, gameCode))
                {
                    await gameService.DisconnectAsync(gameCode, registration.UserId, CancellationToken.None);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task ExecuteCommandAsync(Func<Task> command)
    {
        try
        {
            var userId = Context.User!.GetRequiredUserId().ToString();
            using var lease = await rateLimiter.AcquireAsync(userId, Context.ConnectionAborted);
            if (!lease.IsAcquired)
            {
                await Clients.Caller.CommandRejected(new(
                    "rate_limited",
                    "Você está fazendo ações rápido demais. Tente novamente em instantes."));
                return;
            }

            await command();
        }
        catch (AppException exception)
        {
            await Clients.Caller.CommandRejected(new(exception.Code, UserMessageFor(exception.Code)));
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogUnexpectedCommandFailure(logger, exception, Context.ConnectionId);
            await Clients.Caller.CommandRejected(new(
                "unexpected_error",
                "Não foi possível concluir a ação. Tente novamente."));
        }
    }

    private static string UserMessageFor(string code) => code switch
    {
        "game_not_found" => "Esta sala não está disponível.",
        "not_game_member" => "Você não faz parte desta sala.",
        "host_required" => "Somente o host pode iniciar a partida.",
        "lobby_not_ready" => "Todos os jogadores precisam estar prontos.",
        "invalid_round_count" => "Escolha entre 3 e 6 rodadas.",
        "invalid_phrase_duration" => "Escolha 30, 60 ou 90 segundos para a frase.",
        "invalid_results_duration" => "Escolha 15, 30 ou 60 segundos para a revelação.",
        "invalid_game_mode" => "Escolha um modo de jogo válido.",
        "game_already_started" => "A partida já começou.",
        "phase_expired" => "Essa etapa terminou. O jogo será sincronizado.",
        "invalid_round_phase" => "Essa ação não está disponível agora.",
        "self_vote_forbidden" => "Você não pode votar na própria resposta.",
        "gif_presentation_in_progress" => "Aguarde todos os GIFs serem apresentados.",
        _ => "Não foi possível concluir a ação. Tente novamente."
    };
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Error,
        Message = "Unexpected game command failure for connection {ConnectionId}")]
    private static partial void LogUnexpectedCommandFailure(
        ILogger logger,
        Exception exception,
        string connectionId);
}
