using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Health;
using GifJam.Api.Common.Observability;
using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Data.Cleanup;
using GifJam.Api.Data.Repositories;
using GifJam.Api.Features.AiPhrases;
using GifJam.Api.Features.Auth;
using GifJam.Api.Features.Games;
using GifJam.Api.Features.Games.Interfaces;
using GifJam.Api.Features.Games.Services;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Features.Matchmaking;
using GifJam.Api.Features.Ranking;
using GifJam.Api.GameEngine;
using GifJam.Api.Integrations.Discord;
using GifJam.Api.Integrations.Gemini;
using GifJam.Api.Integrations.Giphy;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Composition;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddGifJamServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd("code", "http_error");
                context.ProblemDetails.Extensions.TryAdd("traceId", TraceContext.GetTraceId(context.HttpContext));
            };
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOptions<GameRetentionOptions>()
            .BindConfiguration(GameRetentionOptions.SectionName)
            .Validate(options => options.RetentionHours > 0, "RetentionHours must be greater than zero.")
            .Validate(options => options.CleanupIntervalMinutes > 0, "CleanupIntervalMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<MatchmakingOptions>()
            .BindConfiguration(MatchmakingOptions.SectionName)
            .Validate(options => options.BatchWindowSeconds is 30 or 60,
                "Matchmaking BatchWindowSeconds must be 30 or 60.")
            .Validate(options => options.ProcessingIntervalSeconds is >= 1 and <= 10,
                "Matchmaking ProcessingIntervalSeconds must be between 1 and 10.")
            .Validate(options => options.DefaultTotalRounds is >= 3 and <= 6,
                "Matchmaking DefaultTotalRounds must be between 3 and 6.")
            .Validate(options => options.DefaultPhraseSubmissionSeconds is 30 or 60 or 90,
                "Matchmaking DefaultPhraseSubmissionSeconds must be 30, 60 or 90.")
            .Validate(options => options.DefaultResultsSeconds is 15 or 30 or 60,
                "Matchmaking DefaultResultsSeconds must be 15, 30 or 60.")
            .ValidateOnStart();

        services.AddOptions<DiscordOptions>()
            .BindConfiguration(DiscordOptions.SectionName)
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.CallbackUrl))
                {
                    options.CallbackUrl = configuration["GIFJAM_DISCORD_CALLBACK_URL"] ?? string.Empty;
                }
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Discord ClientId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "Discord ClientSecret is required.")
            .Validate(options => Uri.TryCreate(options.CallbackUrl, UriKind.Absolute, out _), "Discord CallbackUrl must be absolute.")
            .Validate(options => Uri.TryCreate(options.AuthorizationEndpoint, UriKind.Absolute, out _), "Discord AuthorizationEndpoint must be absolute.")
            .Validate(options => Uri.TryCreate(options.TokenEndpoint, UriKind.Absolute, out _), "Discord TokenEndpoint must be absolute.")
            .Validate(options => Uri.TryCreate(options.UserEndpoint, UriKind.Absolute, out _), "Discord UserEndpoint must be absolute.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(options => options.SigningKey.Length >= 64, "JWT SigningKey must have at least 64 characters.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT Audience is required.")
            .Validate(options => options.LifetimeHours > 0, "JWT LifetimeHours must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<ApplicationUrlOptions>()
            .BindConfiguration(ApplicationUrlOptions.SectionName)
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.FrontendUrl))
                {
                    options.FrontendUrl = configuration["GIFJAM_FRONTEND_URL"] ?? string.Empty;
                }
            })
            .Validate(options => Uri.TryCreate(options.FrontendUrl, UriKind.Absolute, out _), "FrontendUrl must be absolute.")
            .ValidateOnStart();

        services.AddOptions<KlipyOptions>()
            .BindConfiguration(KlipyOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "KLIPY ApiKey is required.")
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "KLIPY BaseUrl must be an absolute HTTPS URL.")
            .Validate(options => options.Locale.Length is >= 2 and <= 16, "KLIPY Locale is invalid.")
            .Validate(options => options.Country.Length == 2, "KLIPY Country must be an ISO alpha-2 code.")
            .ValidateOnStart();

        services.AddOptions<GiphyOptions>()
            .BindConfiguration(GiphyOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "GIPHY ApiKey is required.")
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "GIPHY BaseUrl must be an absolute HTTPS URL.")
            .Validate(options => options.Language.Length is >= 2 and <= 8, "GIPHY Language is invalid.")
            .Validate(options => options.Rating is "g" or "pg" or "pg-13" or "r", "GIPHY Rating is invalid.")
            .ValidateOnStart();

        services.AddOptions<GeminiOptions>()
            .BindConfiguration(GeminiOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "Gemini Model is required.")
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
                "Gemini BaseUrl must be an absolute HTTPS URL.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 30, "Gemini TimeoutSeconds must be between 1 and 30.")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
            {
                bearerOptions.TokenValidationParameters = JwtTokenService.CreateValidationParameters(jwtOptions.Value);
                bearerOptions.Events = new()
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token))
                        {
                            context.Token = context.Request.Cookies[AuthEndpoints.SessionCookieName];
                        }

                        if (string.IsNullOrWhiteSpace(context.Token) &&
                            context.Request.Path.StartsWithSegments("/hubs/game"))
                        {
                            context.Token = context.Request.Query["access_token"];
                        }

                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorization();

        var frontendUrl = configuration["ApplicationUrls:FrontendUrl"];
        frontendUrl = string.IsNullOrWhiteSpace(frontendUrl)
            ? configuration["GIFJAM_FRONTEND_URL"] ?? "http://localhost:4200"
            : frontendUrl;
        services.AddCors(options => options.AddPolicy("frontend", policy =>
            policy.WithOrigins(frontendUrl).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(GameEndpoints.WriteRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "unauthenticated",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(MatchmakingEndpoints.WriteRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                    context.Connection.RemoteIpAddress?.ToString() ??
                    "unauthenticated",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(GifEndpoints.SearchRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unauthenticated",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        services.AddHttpClient<IDiscordClient, DiscordClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(5));
        services.AddHttpClient<KlipyGifProvider>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<KlipyOptions>>().Value;
                client.BaseAddress = new(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .RemoveAllLoggers();
        services.AddHttpClient<GiphyGifProvider>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GiphyOptions>>().Value;
                client.BaseAddress = new(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(5);
            })
            .RemoveAllLoggers();
        services.AddScoped<IGifProvider, CompositeGifProvider>();
        services.AddHttpClient<IAiPhraseProvider, GeminiAiPhraseProvider>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .RemoveAllLoggers();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IRandomizer, CryptoRandomizer>();
        services.AddSingleton<SignalRCommandRateLimiter>();
        services.AddSingleton<GifSelectionTokenService>();
        services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
        services.AddSingleton<IGameLockManager, GameLockManager>();
        services.AddSingleton<GameTelemetry>();
        services.AddSingleton<GameStateProjector>();
        services.AddSingleton<GameConnectionRegistry>();
        services.AddSingleton<IGameRealtimeNotifier, GameRealtimeNotifier>();
        services.AddSingleton<IMatchmakingQueueLock, MatchmakingQueueLock>();
        services.AddSingleton<IMatchmakingRealtimeNotifier, MatchmakingRealtimeNotifier>();
        services.AddScoped<AuthStateService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<GifSearchService>();
        services.AddScoped<AiPhraseGenerationService>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<GameLobbyService>();
        services.AddScoped<GameService>();
        services.AddScoped<IGameService>(serviceProvider => serviceProvider.GetRequiredService<GameService>());
        services.AddScoped<RankingService>();
        services.AddScoped<GameRoundService>();
        services.AddScoped<IGameRoundService>(serviceProvider => serviceProvider.GetRequiredService<GameRoundService>());
        services.AddScoped<GameCoordinator>();
        services.AddScoped<GameRecoveryService>();
        services.AddScoped<GameCleanupService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();

        if (configuration.GetValue("BackgroundServices:Enabled", true))
        {
            services.AddHostedService<GameCleanupWorker>();
            services.AddHostedService<GameRecoveryWorker>();
            services.AddHostedService<RoundScheduler>();
            services.AddHostedService<MatchmakingWorker>();
        }

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

        return services;
    }
}
