using System.Security.Cryptography;
using System.Text.Json;
using GifJam.Api.Common.Errors;
using Microsoft.AspNetCore.DataProtection;

namespace GifJam.Api.Features.Auth;

public sealed class AuthStateService(IDataProtectionProvider dataProtectionProvider)
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private readonly ITimeLimitedDataProtector protector = dataProtectionProvider
        .CreateProtector("GifJam.Auth.DiscordState.v1")
        .ToTimeLimitedDataProtector();

    public string Create(string returnUrl)
    {
        var payload = JsonSerializer.Serialize(new AuthStatePayload(ReturnUrlValidator.Normalize(returnUrl)));
        return protector.Protect(payload, StateLifetime);
    }

    public string ReadReturnUrl(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw InvalidState();
        }

        try
        {
            var json = protector.Unprotect(state);
            var payload = JsonSerializer.Deserialize<AuthStatePayload>(json);
            return ReturnUrlValidator.Normalize(payload?.ReturnUrl);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            throw InvalidState();
        }
    }

    private static ApiException InvalidState() => new(
        "invalid_oauth_state",
        "The authentication request is invalid or expired.",
        StatusCodes.Status400BadRequest);

    private sealed record AuthStatePayload(string ReturnUrl);
}
