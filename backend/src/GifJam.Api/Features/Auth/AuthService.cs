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
    DiscordIdentitySynchronizer identitySynchronizer,
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
        var exchangeCode = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await identitySynchronizer.ExecuteAsUserAsync(
            identity,
            async (user, operationCancellationToken) =>
            {
                dbContext.AuthExchangeCodes.Add(new()
                {
                    CodeHash = Hash(exchangeCode),
                    User = user,
                    UserId = user.Id,
                    ExpiresAt = now.AddSeconds(60)
                });

                await dbContext.SaveChangesAsync(operationCancellationToken);
                return true;
            },
            cancellationToken);

        return new(exchangeCode, returnUrl);
    }

    public async Task<AuthSessionResult> ExchangeAsync(
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
        return new(token.AccessToken, token.ExpiresAt, await MapUserAsync(user, cancellationToken));
    }

    public async Task<AuthUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(savedUser => savedUser.Id == userId, cancellationToken)
            ?? throw new ApiException("user_not_found", "The authenticated user was not found.", StatusCodes.Status401Unauthorized);

        return await MapUserAsync(user, cancellationToken);
    }

    public async Task DeleteAccountAsync(Guid userId, string confirmation, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation?.Trim(), "EXCLUIR", StringComparison.OrdinalIgnoreCase))
        {
            throw new ApiException(
                "account_deletion_confirmation_required",
                "Type EXCLUIR to confirm account deletion.",
                StatusCodes.Status400BadRequest);
        }

        var userExists = await dbContext.Users.AnyAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (!userExists)
        {
            // Deletion is intentionally idempotent. If a previous request
            // removed the row but the client did not receive the response,
            // this request must still clear the session cookie.
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var hostedGameIds = await dbContext.Games
            .Where(game => game.HostUserId == userId)
            .Select(game => game.Id)
            .ToArrayAsync(cancellationToken);

        // Hosted games own their rounds and submissions. Removing them avoids
        // leaving a foreign key pointing at a deleted host account.
        if (hostedGameIds.Length > 0)
        {
            await dbContext.Games
                .Where(game => hostedGameIds.Contains(game.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.GifVotes.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.PhraseVotes.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.GifSubmissions.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Phrases.Where(item => item.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UserId, (Guid?)null), cancellationToken);
        await dbContext.GamePlayers.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.MatchmakingTickets.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.AuthExchangeCodes.Where(item => item.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.Users.Where(item => item.Id == userId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private async Task<AuthUserResponse> MapUserAsync(User user, CancellationToken cancellationToken)
    {
        int? rank = user.TotalScore > 0
            ? 1 + await dbContext.Users.CountAsync(
                candidate => candidate.TotalScore > user.TotalScore,
                cancellationToken)
            : null;

        return new(
            user.Id,
            user.DiscordId,
            user.Username,
            user.DisplayName,
            user.AvatarUrl,
            user.TotalScore,
            rank);
    }

    private static ApiException InvalidExchangeCode() => new(
        "invalid_exchange_code",
        "The authentication code is invalid, expired, or already used.",
        StatusCodes.Status400BadRequest);
}
