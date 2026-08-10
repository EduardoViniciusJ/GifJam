using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Health;
using GifJam.Api.Common.Random;
using GifJam.Api.Common.Time;
using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Observability;
using GifJam.Api.Data;
using GifJam.Api.Data.Cleanup;
using GifJam.Api.Features.Auth;
using GifJam.Api.Features.Gifs;
using GifJam.Api.Features.Games;
using GifJam.Api.GameEngine;
using GifJam.Api.Integrations.Discord;
using GifJam.Api.Integrations.Klipy;
using GifJam.Api.Realtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Logging.Configure(options => options.ActivityTrackingOptions =
    ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", "http_error");
        context.ProblemDetails.Extensions.TryAdd("traceId", TraceContext.GetTraceId(context.HttpContext));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IRandomizer, CryptoRandomizer>();
builder.Services.AddOptions<GameRetentionOptions>()
    .BindConfiguration(GameRetentionOptions.SectionName)
    .Validate(options => options.RetentionHours > 0, "RetentionHours must be greater than zero.")
    .Validate(options => options.CleanupIntervalMinutes > 0, "CleanupIntervalMinutes must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddScoped<GameCleanupService>();
builder.Services.AddOptions<DiscordOptions>()
    .BindConfiguration(DiscordOptions.SectionName)
    .PostConfigure(options =>
    {
        if (string.IsNullOrWhiteSpace(options.CallbackUrl))
        {
            options.CallbackUrl = builder.Configuration["GIFJAM_DISCORD_CALLBACK_URL"] ?? string.Empty;
        }
    })
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Discord ClientId is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "Discord ClientSecret is required.")
    .Validate(options => Uri.TryCreate(options.CallbackUrl, UriKind.Absolute, out _), "Discord CallbackUrl must be absolute.")
    .Validate(options => Uri.TryCreate(options.AuthorizationEndpoint, UriKind.Absolute, out _), "Discord AuthorizationEndpoint must be absolute.")
    .Validate(options => Uri.TryCreate(options.TokenEndpoint, UriKind.Absolute, out _), "Discord TokenEndpoint must be absolute.")
    .Validate(options => Uri.TryCreate(options.UserEndpoint, UriKind.Absolute, out _), "Discord UserEndpoint must be absolute.")
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options => options.SigningKey.Length >= 64, "JWT SigningKey must have at least 64 characters.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT Issuer is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT Audience is required.")
    .Validate(options => options.LifetimeHours > 0, "JWT LifetimeHours must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddOptions<ApplicationUrlOptions>()
    .BindConfiguration(ApplicationUrlOptions.SectionName)
    .PostConfigure(options =>
    {
        if (string.IsNullOrWhiteSpace(options.FrontendUrl))
        {
            options.FrontendUrl = builder.Configuration["GIFJAM_FRONTEND_URL"] ?? string.Empty;
        }
    })
    .Validate(options => Uri.TryCreate(options.FrontendUrl, UriKind.Absolute, out _), "FrontendUrl must be absolute.")
    .ValidateOnStart();
builder.Services.AddOptions<KlipyOptions>()
    .BindConfiguration(KlipyOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "KLIPY ApiKey is required.")
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
        "KLIPY BaseUrl must be an absolute HTTPS URL.")
    .Validate(options => options.Locale.Length is >= 2 and <= 16, "KLIPY Locale is invalid.")
    .Validate(options => options.Country.Length == 2, "KLIPY Country must be an ISO alpha-2 code.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        bearerOptions.TokenValidationParameters = JwtTokenService.CreateValidationParameters(jwtOptions.Value);
        bearerOptions.Events = new()
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs/game"))
                {
                    context.Token = context.Request.Query["access_token"];
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
{
    var configuredFrontendUrl = builder.Configuration["ApplicationUrls:FrontendUrl"];
    var frontendUrl = string.IsNullOrWhiteSpace(configuredFrontendUrl)
        ? builder.Configuration["GIFJAM_FRONTEND_URL"] ?? "http://localhost:4200"
        : configuredFrontendUrl;
    policy.WithOrigins(frontendUrl).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(AuthEndpoints.RateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter(GameEndpoints.WriteRateLimitPolicy, limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
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
builder.Services.AddHttpClient<IDiscordClient, DiscordClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<IGifProvider, KlipyGifProvider>((services, client) =>
    {
        var options = services.GetRequiredService<IOptions<KlipyOptions>>().Value;
        client.BaseAddress = new(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .RemoveAllLoggers();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<GifSelectionTokenService>();
builder.Services.AddScoped<GifSearchService>();
builder.Services.AddSingleton<IGameCodeGenerator, GameCodeGenerator>();
builder.Services.AddSingleton<IGameLockManager, GameLockManager>();
builder.Services.AddSingleton<GameTelemetry>();
builder.Services.AddSingleton<GameStateProjector>();
builder.Services.AddSingleton<GameConnectionRegistry>();
builder.Services.AddSingleton<IGameRealtimeNotifier, GameRealtimeNotifier>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<GameCoordinator>();
builder.Services.AddScoped<GameRecoveryService>();
if (builder.Configuration.GetValue("BackgroundServices:Enabled", true))
{
    builder.Services.AddHostedService<GameCleanupWorker>();
    builder.Services.AddHostedService<GameRecoveryWorker>();
    builder.Services.AddHostedService<RoundScheduler>();
}
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var problemDetailsService = statusCodeContext.HttpContext.RequestServices
        .GetRequiredService<IProblemDetailsService>();

    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = statusCodeContext.HttpContext,
        ProblemDetails = new()
        {
            Status = response.StatusCode,
            Title = ReasonPhrases.GetReasonPhrase(response.StatusCode),
            Detail = "The request could not be completed."
        }
    });
});

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).ExcludeFromDescription();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
}).ExcludeFromDescription();

app.MapGet("/", () => Results.Ok(new { name = "GifJam API", status = "running" }))
    .ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapGifEndpoints();
app.MapHub<GameHub>("/hubs/game");

app.Run();

public partial class Program;
