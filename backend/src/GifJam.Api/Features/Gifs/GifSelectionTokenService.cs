using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GifJam.Api.Common.Auth;
using GifJam.Api.Common.Errors;
using GifJam.Api.Common.Time;
using GifJam.Api.Integrations.Klipy;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Features.Gifs;

public sealed class GifSelectionTokenService(
    IOptions<JwtOptions> jwtOptions,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(2);
    private readonly byte[] signingKey = DeriveSigningKey(jwtOptions.Value.SigningKey);

    public string Create(string gameCode, GifProviderItem item)
    {
        var payload = new GifSelectionPayload(
            gameCode,
            "klipy",
            item.ExternalId,
            item.Description,
            item.PreviewUrl,
            item.MediaUrl,
            item.Width,
            item.Height,
            item.PreviewWidth,
            item.PreviewHeight,
            item.SourceUrl,
            item.Attribution,
            clock.UtcNow.Add(TokenLifetime));
        var encodedPayload = WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signature = WebEncoders.Base64UrlEncode(HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(encodedPayload)));
        return $"{encodedPayload}.{signature}";
    }

    public GifSelectionPayload Validate(string token, string expectedGameCode)
    {
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw InvalidToken();
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
            throw InvalidToken();
        }

        if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
        {
            throw InvalidToken();
        }

        GifSelectionPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<GifSelectionPayload>(
                WebEncoders.Base64UrlDecode(parts[0]),
                JsonOptions) ?? throw InvalidToken();
        }
        catch (JsonException)
        {
            throw InvalidToken();
        }
        catch (FormatException)
        {
            throw InvalidToken();
        }

        if (payload.ExpiresAt <= clock.UtcNow)
        {
            throw new ApiException(
                "gif_selection_expired",
                "The GIF selection has expired. Search again before submitting.",
                StatusCodes.Status409Conflict);
        }

        if (!string.Equals(payload.GameCode, expectedGameCode, StringComparison.Ordinal) ||
            !string.Equals(payload.Provider, "klipy", StringComparison.Ordinal))
        {
            throw InvalidToken();
        }

        return payload;
    }

    private static byte[] DeriveSigningKey(string jwtSigningKey) =>
        HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(jwtSigningKey),
            "GifJam.GifSelection.v1"u8.ToArray());

    private static ApiException InvalidToken() => new(
        "invalid_gif_selection",
        "The GIF selection token is invalid.",
        StatusCodes.Status400BadRequest);
}

public sealed record GifSelectionPayload(
    string GameCode,
    string Provider,
    string ExternalId,
    string Description,
    string PreviewUrl,
    string MediaUrl,
    int Width,
    int Height,
    int PreviewWidth,
    int PreviewHeight,
    string SourceUrl,
    string Attribution,
    DateTimeOffset ExpiresAt);
