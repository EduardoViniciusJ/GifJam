using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Time;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Features.Auth;

public sealed class AuthStateService(
    IOptions<JwtOptions> jwtOptions,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(5);
    private readonly byte[] signingKey = DeriveSigningKey(jwtOptions.Value.SigningKey);

    public string Create(string returnUrl)
    {
        var payload = new AuthStatePayload(
            ReturnUrlValidator.Normalize(returnUrl),
            clock.UtcNow.Add(StateLifetime));
        var encodedPayload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signature = WebEncoders.Base64UrlEncode(HMACSHA256.HashData(
            signingKey,
            Encoding.ASCII.GetBytes(encodedPayload)));
        return $"{encodedPayload}.{signature}";
    }

    public string ReadReturnUrl(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw InvalidState();
        }

        var parts = state.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw InvalidState();
        }

        byte[] suppliedSignature;
        byte[] expectedSignature;
        try
        {
            suppliedSignature = WebEncoders.Base64UrlDecode(parts[1]);
            expectedSignature = HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(parts[0]));
        }
        catch (FormatException)
        {
            throw InvalidState();
        }

        if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
        {
            throw InvalidState();
        }

        AuthStatePayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<AuthStatePayload>(
                WebEncoders.Base64UrlDecode(parts[0]),
                JsonOptions) ?? throw InvalidState();
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw InvalidState();
        }

        if (payload.ExpiresAt <= clock.UtcNow)
        {
            throw InvalidState();
        }

        return ReturnUrlValidator.Normalize(payload.ReturnUrl);
    }

    private static byte[] DeriveSigningKey(string jwtSigningKey) =>
        HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtSigningKey),
            "GifJam.Auth.DiscordState.v2"u8.ToArray());

    private static ApiException InvalidState() => new(
        "invalid_oauth_state",
        "The authentication request is invalid or expired.",
        StatusCodes.Status400BadRequest);

    private sealed record AuthStatePayload(string ReturnUrl, DateTimeOffset ExpiresAt);
}
