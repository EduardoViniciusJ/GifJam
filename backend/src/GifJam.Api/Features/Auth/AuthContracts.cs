namespace GifJam.Api.Features.Auth;

public sealed record AuthExchangeRequest(string Code);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthUserResponse User);

public sealed record AuthUserResponse(
    Guid Id,
    string DiscordId,
    string Username,
    string DisplayName,
    string? AvatarUrl);

public sealed record AuthCallbackResult(string ExchangeCode, string ReturnUrl);
