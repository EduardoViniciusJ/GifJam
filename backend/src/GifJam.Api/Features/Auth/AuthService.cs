using System.Security.Cryptography;
using System.Text;
using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Time;
using GifJam.Api.Data;
using GifJam.Api.Domain.Entities;
using GifJam.Api.Integrations.Discord;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GifJam.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext dbContext,
    IDiscordClient discordClient,
    AuthStateService stateService,
    JwtTokenService jwtTokenService,
    IClock clock,
    IOptions<DiscordOptions> discordOptions,
    IOptions<ApplicationUrlOptions> applicationUrls)
{
    public Uri CreateAuthorizationUri(string? returnUrl)
    {
        var normalizedReturnUrl = ReturnUrlValidator.Normalize(returnUrl);
        var state = stateService.Create(normalizedReturnUrl);
        var url = QueryHelpers.AddQueryString(discordOptions.Value.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = discordOptions.Value.ClientId,
            ["redirect_uri"] = discordOptions.Value.CallbackUrl,
            ["response_type"] = "code",
            ["scope"] = "identify",
            ["state"] = state
        });

        return new(url);
    }

    public async Task<AuthCallbackResult> CompleteDiscordLoginAsync(
        string authorizationCode,
        string? state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new ApiException(
                "missing_authorization_code",
                "Discord did not provide an authorization code.",
                StatusCodes.Status400BadRequest);
        }

        var returnUrl = stateService.ReadReturnUrl(state);
        var identity = await discordClient.GetIdentityAsync(authorizationCode, cancellationToken);
        var now = clock.UtcNow;
        var user = await dbContext.Users.SingleOrDefaultAsync(
            savedUser => savedUser.DiscordId == identity.DiscordId,
            cancellationToken);

        if (user is null)
        {
            user = new()
            {
                DiscordId = identity.DiscordId,
                CreatedAt = now
            };
            dbContext.Users.Add(user);
        }

        user.Username = identity.Username;
        user.DisplayName = identity.DisplayName;
        user.AvatarUrl = identity.AvatarUrl;
        user.UpdatedAt = now;

        var exchangeCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        dbContext.AuthExchangeCodes.Add(new()
        {
            CodeHash = Hash(exchangeCode),
            User = user,
            UserId = user.Id,
            ExpiresAt = now.AddSeconds(60)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new(exchangeCode, returnUrl);
    }

    public async Task<AuthResponse> ExchangeAsync(
        AuthExchangeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw InvalidExchangeCode();
        }

        var now = clock.UtcNow;
        var hash = Hash(request.Code);
        var candidate = await dbContext.AuthExchangeCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(code => code.CodeHash == hash, cancellationToken);

        if (candidate is null || candidate.ExpiresAt <= now || candidate.ConsumedAt is not null)
        {
            throw InvalidExchangeCode();
        }

        var consumed = await dbContext.AuthExchangeCodes
            .Where(code => code.Id == candidate.Id && code.ConsumedAt == null && code.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.ConsumedAt, now), cancellationToken);
        if (consumed != 1)
        {
            throw InvalidExchangeCode();
        }

        var user = await dbContext.Users.SingleAsync(user => user.Id == candidate.UserId, cancellationToken);
        var token = jwtTokenService.Create(user);
        return new(token.AccessToken, token.ExpiresAt, MapUser(user));
    }

    public async Task<AuthUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(savedUser => savedUser.Id == userId, cancellationToken)
            ?? throw new ApiException("user_not_found", "The authenticated user was not found.", StatusCodes.Status401Unauthorized);

        return MapUser(user);
    }

    public string ReadReturnUrl(string? state) => stateService.ReadReturnUrl(state);

    public Uri CreateFrontendCallbackUri(string returnUrl, string? exchangeCode = null, string? error = null)
    {
        var frontendBase = new Uri(applicationUrls.Value.FrontendUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var callback = new Uri(frontendBase, "auth/callback").ToString();
        var query = new Dictionary<string, string?> { ["returnUrl"] = ReturnUrlValidator.Normalize(returnUrl) };
        if (!string.IsNullOrWhiteSpace(exchangeCode))
        {
            query["code"] = exchangeCode;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            query["error"] = error;
        }

        return new(QueryHelpers.AddQueryString(callback, query));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static AuthUserResponse MapUser(User user) => new(
        user.Id,
        user.DiscordId,
        user.Username,
        user.DisplayName,
        user.AvatarUrl);

    private static ApiException InvalidExchangeCode() => new(
        "invalid_exchange_code",
        "The authentication code is invalid, expired, or already used.",
        StatusCodes.Status400BadRequest);
}
