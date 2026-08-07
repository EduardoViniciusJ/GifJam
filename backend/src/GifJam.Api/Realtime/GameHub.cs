using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Features.Games;
using GifJam.Api.GameEngine;
using GifJam.Api.Realtime.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GifJam.Api.Realtime;

[Authorize]
public sealed class GameHub(
    GameService gameService,
    GameCoordinator gameCoordinator,
    GameConnectionRegistry connectionRegistry) : Hub<IGameClient>
{
    public async Task SubscribeGame(string gameCode)
    {
        await ExecuteCommandAsync(async () =>
        {
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
        var userId = Context.User!.GetRequiredUserId();
        await gameService.SetReadyAsync(gameCode, userId, isReady, Context.ConnectionAborted);
    });

    public Task RequestSync(string gameCode) => ExecuteCommandAsync(async () =>
    {
        var userId = Context.User!.GetRequiredUserId();
        var snapshot = await gameService.GetAsync(gameCode, userId, Context.ConnectionAborted);
        await Clients.Caller.StateSynced(snapshot);
    });

    public Task StartGame(string gameCode) => ExecuteCommandAsync(async () =>
    {
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.StartGameAsync(gameCode, userId, Context.ConnectionAborted);
    });

    public Task SubmitPhrase(string gameCode, string text) => ExecuteCommandAsync(async () =>
    {
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.SubmitPhraseAsync(gameCode, userId, text, Context.ConnectionAborted);
    });

    public Task VotePhrase(string gameCode, Guid phraseId) => ExecuteCommandAsync(async () =>
    {
        var userId = Context.User!.GetRequiredUserId();
        await gameCoordinator.VotePhraseAsync(gameCode, userId, phraseId, Context.ConnectionAborted);
    });

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var registration = connectionRegistry.Remove(Context.ConnectionId);
        if (registration is not null)
        {
            foreach (var gameCode in registration.GameCodes.Keys)
            {
                await gameService.DisconnectAsync(gameCode, registration.UserId, CancellationToken.None);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task ExecuteCommandAsync(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (ApiException exception)
        {
            await Clients.Caller.CommandRejected(new(exception.Code, exception.Message));
        }
    }
}
