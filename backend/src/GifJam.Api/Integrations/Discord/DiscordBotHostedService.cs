using Discord;
using Discord.Rest;
using Discord.WebSocket;
using GifJam.Api.Common.Auth;
using GifJam.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Integrations.Discord;

public sealed partial class DiscordBotHostedService(
    IServiceScopeFactory scopeFactory,
    DiscordCommandRateLimiter rateLimiter,
    IOptions<DiscordOptions> discordOptions,
    IOptions<ApplicationUrlOptions> applicationUrls,
    ILogger<DiscordBotHostedService> logger) : BackgroundService
{
    public const string CreateCommandName = "gifjam-create";
    public const string CreateCommandDescription =
        "Cria uma sala privada do GifJam e envia o código e o link.";
    public const string RoomCommandName = "gifjam-room";
    public const string RoomCommandDescription =
        "Mostra novamente o código e o link da sua sala atual.";
    public const string CloseCommandName = "gifjam-close";
    public const string CloseCommandDescription =
        "Encerra a sala do GifJam que você está hospedando.";
    public const string HelpCommandName = "gifjam-help";
    public const string HelpCommandDescription =
        "Mostra os comandos disponíveis do GifJam.";

    private readonly SemaphoreSlim commandRegistrationLock = new(1, 1);
    private DiscordSocketClient? client;
    private bool commandsRegistered;
    private CancellationToken stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.stoppingToken = stoppingToken;
        if (!discordOptions.Value.BotEnabled)
        {
            LogBotDisabled(logger);
            return;
        }

        client = new(new DiscordSocketConfig
        {
            AlwaysDownloadUsers = false,
            GatewayIntents = GatewayIntents.Guilds,
            LogGatewayIntentWarnings = true,
            LogLevel = LogSeverity.Info
        });
        client.Log += HandleDiscordLogAsync;
        client.Ready += HandleReadyAsync;
        client.SlashCommandExecuted += HandleSlashCommandAsync;

        try
        {
            await client.LoginAsync(TokenType.Bot, discordOptions.Value.BotToken)
                .WaitAsync(stoppingToken);
            await client.StartAsync().WaitAsync(stoppingToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            client.Log -= HandleDiscordLogAsync;
            client.Ready -= HandleReadyAsync;
            client.SlashCommandExecuted -= HandleSlashCommandAsync;

            try
            {
                await client.StopAsync();
                await client.LogoutAsync();
            }
            catch (Exception exception)
            {
                LogDisconnectFailed(logger, exception);
            }

            client.Dispose();
            client = null;
        }
    }

    private async Task HandleReadyAsync()
    {
        var socketClient = client;
        if (socketClient is null)
        {
            return;
        }

        await socketClient.SetStatusAsync(UserStatus.Online);
        await socketClient.SetGameAsync(discordOptions.Value.BotActivity);

        await commandRegistrationLock.WaitAsync(stoppingToken);
        try
        {
            if (commandsRegistered)
            {
                return;
            }

            var developmentGuildId = discordOptions.Value.DevelopmentGuildId;
            var commands = BuildCommands(isGlobal: developmentGuildId is null);
            if (developmentGuildId is ulong guildId)
            {
                var guild = socketClient.GetGuild(guildId)
                    ?? throw new InvalidOperationException("The Discord bot is not installed in the configured development guild.");
                await guild.BulkOverwriteApplicationCommandAsync(commands);
                LogDevelopmentCommandsRegistered(logger, guildId);
            }
            else
            {
                await ((global::Discord.IDiscordClient)socketClient)
                    .BulkOverwriteGlobalApplicationCommand(commands);
                LogGlobalCommandsRegistered(logger);
            }

            commandsRegistered = true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogCommandRegistrationFailed(logger, exception);
        }
        finally
        {
            commandRegistrationLock.Release();
        }
    }

    private Task HandleSlashCommandAsync(SocketSlashCommand command) =>
        command.Data.Name switch
        {
            CreateCommandName => HandleCreateCommandAsync(command),
            RoomCommandName => HandleRoomCommandAsync(command),
            CloseCommandName => HandleCloseCommandAsync(command),
            HelpCommandName => HandleHelpCommandAsync(command),
            _ => Task.CompletedTask
        };

    private async Task HandleHelpCommandAsync(SocketSlashCommand command)
    {
        var embed = new EmbedBuilder()
            .WithColor(new Color(124, 58, 237))
            .WithTitle("Comandos do GifJam")
            .WithDescription(
                "`/gifjam-create` — Cria uma sala privada ou republica sua sala atual.\n" +
                "`/gifjam-room` — Mostra novamente o código e o link da sua sala.\n" +
                "`/gifjam-close` — Encerra a sala que você está hospedando.\n" +
                "`/gifjam-help` — Exibe esta ajuda.")
            .WithFooter("As mensagens de sala ficam no canal; a ajuda e os erros são privados.")
            .Build();

        try
        {
            await command.RespondAsync(embed: embed, ephemeral: true);
        }
        catch (Exception exception)
        {
            LogInteractionAcknowledgementFailed(logger, command.Id, exception);
        }
    }

    private async Task HandleCloseCommandAsync(SocketSlashCommand command)
    {
        var discordUserId = await BeginRoomCommandAsync(command);
        if (discordUserId is null)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var roomService = scope.ServiceProvider.GetRequiredService<DiscordBotRoomService>();
            var roomCode = await roomService.CloseHostedLobbyAsync(discordUserId, stoppingToken);
            if (roomCode is null)
            {
                await WritePrivateResponseAsync(
                    command,
                    "Você não possui uma sala aguardando jogadores para encerrar.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithColor(new Color(239, 68, 68))
                .WithTitle("Sala encerrada")
                .WithDescription($"A sala `{roomCode}` foi encerrada pelo host.")
                .Build();
            await command.Channel.SendMessageAsync(
                text: $"Sala de {MentionUtils.MentionUser(command.User.Id)}",
                allowedMentions: AllowedMentions.None,
                embed: embed);
            await DeletePrivateAcknowledgementAsync(command);
            LogRoomClosed(logger, discordUserId, roomCode);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await HandleRoomCommandFailureAsync(
                command,
                exception,
                "Não consegui encerrar sua sala agora. Tente novamente em alguns instantes.");
        }
    }

    private async Task HandleRoomCommandAsync(SocketSlashCommand command)
    {
        var discordUserId = await BeginRoomCommandAsync(command);
        if (discordUserId is null)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var roomService = scope.ServiceProvider.GetRequiredService<DiscordBotRoomService>();
            var room = await roomService.FindHostedLobbyAsync(discordUserId, stoppingToken);
            if (room is null)
            {
                await WritePrivateResponseAsync(
                    command,
                    "Você não possui uma sala aguardando jogadores. Use `/gifjam-create` para criar uma.");
                return;
            }

            await PublishRoomAsync(
                command,
                room,
                "Sua sala atual",
                "Aqui estão novamente o código e o link da sua sala.");
            await DeletePrivateAcknowledgementAsync(command);
            LogRoomReady(logger, discordUserId, room.Code, room.WasReused);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await HandleRoomCommandFailureAsync(
                command,
                exception,
                "Não consegui localizar sua sala agora. Tente novamente em alguns instantes.");
        }
    }

    private async Task HandleCreateCommandAsync(SocketSlashCommand command)
    {
        var discordUserId = await BeginRoomCommandAsync(command);
        if (discordUserId is null)
        {
            return;
        }

        try
        {
            var identity = CreateIdentity(command, discordUserId);

            await using var scope = scopeFactory.CreateAsyncScope();
            var roomService = scope.ServiceProvider.GetRequiredService<DiscordBotRoomService>();
            var room = await roomService.CreateOrReuseAsync(identity, stoppingToken);
            var title = room.WasReused ? "Sua sala já está aberta" : "Sala criada";
            var description = room.WasReused
                ? "Você já tinha uma sala aguardando jogadores. Aqui está ela novamente."
                : "A sala é privada. Compartilhe o código ou use o botão para entrar.";
            await PublishRoomAsync(command, room, title, description);
            await DeletePrivateAcknowledgementAsync(command);
            LogRoomReady(logger, discordUserId, room.Code, room.WasReused);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await HandleRoomCommandFailureAsync(
                command,
                exception,
                "Não consegui criar a sala agora. Tente novamente em alguns instantes.");
        }
    }

    private async Task<string?> BeginRoomCommandAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeferAsync(ephemeral: true);
        }
        catch (Exception exception)
        {
            LogInteractionAcknowledgementFailed(logger, command.Id, exception);
            return null;
        }

        try
        {
            if (command.GuildId is null)
            {
                await WritePrivateResponseAsync(
                    command,
                    "Esse comando só pode ser usado dentro de um servidor.");
                return null;
            }

            var discordUserId = command.User.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (await rateLimiter.TryAcquireAsync(discordUserId, stoppingToken))
            {
                return discordUserId;
            }

            await WritePrivateResponseAsync(
                command,
                "Você usou os comandos de sala rápido demais. Aguarde um minuto e tente novamente.");
            return null;
        }
        catch (Exception exception)
        {
            LogErrorResponseFailed(logger, command.Id, exception);
            return null;
        }
    }

    private async Task PublishRoomAsync(
        SocketSlashCommand command,
        DiscordBotRoomResult room,
        string title,
        string description)
    {
        var visibility = room.Visibility == RoomVisibility.Public ? "Pública" : "Privada";
        var mode = room.Mode == GameMode.Classic ? "clássico" : "frases aleatórias por IA";
        var embed = new EmbedBuilder()
            .WithColor(new Color(124, 58, 237))
            .WithTitle(title)
            .WithDescription(description)
            .AddField("Código", $"`{room.Code}`", inline: true)
            .AddField("Visibilidade", visibility, inline: true)
            .AddField(
                "Configuração",
                $"{room.TotalRounds} rodadas · {room.PhraseSubmissionSeconds}s por frase · " +
                $"{room.ResultsSeconds}s de resultados · modo {mode}")
            .WithFooter("O host inicia a partida quando todos estiverem prontos.")
            .Build();
        var components = new ComponentBuilder()
            .WithButton("Entrar na sala", style: ButtonStyle.Link, url: CreateRoomUri(room.Code).ToString())
            .Build();

        // Discord preserves the ephemeral flag on the first follow-up after
        // an ephemeral defer. Send the success message through the channel
        // so it remains public, then remove only the private loading state.
        await command.Channel.SendMessageAsync(
            text: $"Sala de {MentionUtils.MentionUser(command.User.Id)}",
            allowedMentions: AllowedMentions.None,
            components: components,
            embed: embed);
    }

    private async Task DeletePrivateAcknowledgementAsync(SocketSlashCommand command)
    {
        try
        {
            await command.DeleteOriginalResponseAsync();
        }
        catch (Exception exception)
        {
            LogAcknowledgementDeleteFailed(logger, command.Id, exception);
        }
    }

    private async Task HandleRoomCommandFailureAsync(
        SocketSlashCommand command,
        Exception exception,
        string errorMessage)
    {
        LogRoomCommandFailed(logger, command.Id, command.User.Id, exception);

        try
        {
            await WritePrivateResponseAsync(command, errorMessage);
        }
        catch (Exception responseException)
        {
            LogErrorResponseFailed(logger, command.Id, responseException);
        }
    }

    private static DiscordIdentity CreateIdentity(SocketSlashCommand command, string discordUserId)
    {
        var socketUser = command.User as SocketUser;
        return new(
            discordUserId,
            command.User.Username,
            string.IsNullOrWhiteSpace(socketUser?.GlobalName)
                ? command.User.Username
                : socketUser.GlobalName,
            command.User.GetAvatarUrl(ImageFormat.Png, 128));
    }

    private Uri CreateRoomUri(string roomCode)
    {
        var frontendBase = new Uri(
            applicationUrls.Value.FrontendUrl.TrimEnd('/') + "/",
            UriKind.Absolute);
        return new(frontendBase, $"sala/{Uri.EscapeDataString(roomCode)}");
    }

    private static ApplicationCommandProperties[] BuildCommands(bool isGlobal) =>
    [
        BuildCommand(CreateCommandName, CreateCommandDescription, isGlobal),
        BuildCommand(RoomCommandName, RoomCommandDescription, isGlobal),
        BuildCommand(CloseCommandName, CloseCommandDescription, isGlobal),
        BuildCommand(HelpCommandName, HelpCommandDescription, isGlobal)
    ];

    private static SlashCommandProperties BuildCommand(
        string name,
        string description,
        bool isGlobal)
    {
        var builder = new SlashCommandBuilder()
            .WithName(name)
            .WithDescription(description);

        if (isGlobal)
        {
            builder
                .WithIntegrationTypes(ApplicationIntegrationType.GuildInstall)
                .WithContextTypes(InteractionContextType.Guild);
        }

        return builder.Build();
    }

    private static Task<RestInteractionMessage> WritePrivateResponseAsync(
        SocketSlashCommand command,
        string message) =>
        command.ModifyOriginalResponseAsync(properties => properties.Content = message);

    private Task HandleDiscordLogAsync(LogMessage message)
    {
        var logLevel = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Debug
        };

        switch (logLevel)
        {
            case LogLevel.Critical:
                LogDiscordCritical(logger, message.Source, message.Message, message.Exception);
                break;
            case LogLevel.Error:
                LogDiscordError(logger, message.Source, message.Message, message.Exception);
                break;
            case LogLevel.Warning:
                LogDiscordWarning(logger, message.Source, message.Message, message.Exception);
                break;
            case LogLevel.Information:
                LogDiscordInformation(logger, message.Source, message.Message, message.Exception);
                break;
            case LogLevel.Trace:
                LogDiscordTrace(logger, message.Source, message.Message, message.Exception);
                break;
            default:
                LogDiscordDebug(logger, message.Source, message.Message, message.Exception);
                break;
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Information, Message = "Discord bot is disabled")]
    private static partial void LogBotDisabled(ILogger logger);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning, Message = "Discord bot did not disconnect cleanly")]
    private static partial void LogDisconnectFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Registered Discord commands in development guild {GuildId}")]
    private static partial void LogDevelopmentCommandsRegistered(ILogger logger, ulong guildId);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Information, Message = "Registered global Discord commands")]
    private static partial void LogGlobalCommandsRegistered(ILogger logger);

    [LoggerMessage(EventId = 3104, Level = LogLevel.Error, Message = "Discord command registration failed")]
    private static partial void LogCommandRegistrationFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3105, Level = LogLevel.Warning, Message = "Discord interaction {InteractionId} could not be acknowledged")]
    private static partial void LogInteractionAcknowledgementFailed(ILogger logger, ulong interactionId, Exception exception);

    [LoggerMessage(EventId = 3106, Level = LogLevel.Debug, Message = "Discord interaction {InteractionId} left its private acknowledgement visible")]
    private static partial void LogAcknowledgementDeleteFailed(ILogger logger, ulong interactionId, Exception exception);

    [LoggerMessage(EventId = 3107, Level = LogLevel.Information, Message = "Discord user {DiscordUserId} received room {RoomCode}; reused: {WasReused}")]
    private static partial void LogRoomReady(ILogger logger, string discordUserId, string roomCode, bool wasReused);

    [LoggerMessage(EventId = 3108, Level = LogLevel.Error, Message = "Discord room command {InteractionId} failed for user {DiscordUserId}")]
    private static partial void LogRoomCommandFailed(ILogger logger, ulong interactionId, ulong discordUserId, Exception exception);

    [LoggerMessage(EventId = 3109, Level = LogLevel.Warning, Message = "Discord interaction {InteractionId} could not receive its error response")]
    private static partial void LogErrorResponseFailed(ILogger logger, ulong interactionId, Exception exception);

    [LoggerMessage(EventId = 3110, Level = LogLevel.Critical, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordCritical(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3111, Level = LogLevel.Error, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordError(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3112, Level = LogLevel.Warning, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordWarning(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3113, Level = LogLevel.Information, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordInformation(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3114, Level = LogLevel.Trace, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordTrace(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3115, Level = LogLevel.Debug, Message = "Discord {Source}: {Message}")]
    private static partial void LogDiscordDebug(ILogger logger, string source, string message, Exception? exception);

    [LoggerMessage(EventId = 3116, Level = LogLevel.Information, Message = "Discord user {DiscordUserId} closed room {RoomCode}")]
    private static partial void LogRoomClosed(ILogger logger, string discordUserId, string roomCode);
}
