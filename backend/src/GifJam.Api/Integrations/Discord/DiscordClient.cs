using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GifJam.Api.Common.Errors;
using Microsoft.Extensions.Options;

namespace GifJam.Api.Integrations.Discord;

public sealed partial class DiscordClient(
    HttpClient httpClient,
    IOptions<DiscordOptions> options,
    ILogger<DiscordClient> logger) : IDiscordClient
{
    public async Task<DiscordIdentity> GetIdentityAsync(
        string authorizationCode,
        CancellationToken cancellationToken)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.Value.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.Value.ClientId,
                ["client_secret"] = options.Value.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["redirect_uri"] = options.Value.CallbackUrl
            })
        };
        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            LogDiscordFailure(logger, "token", (int)tokenResponse.StatusCode);
            throw new ApiException(
                "discord_exchange_failed",
                "Discord authentication could not be completed.",
                StatusCodes.Status502BadGateway);
        }

        var token = await tokenResponse.Content.ReadFromJsonAsync<DiscordTokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new ApiException(
                "discord_invalid_response",
                "Discord returned an invalid authentication response.",
                StatusCodes.Status502BadGateway);
        }

        using var identityRequest = new HttpRequestMessage(HttpMethod.Get, options.Value.UserEndpoint);
        identityRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var identityResponse = await httpClient.SendAsync(identityRequest, cancellationToken);
        if (!identityResponse.IsSuccessStatusCode)
        {
            LogDiscordFailure(logger, "identity", (int)identityResponse.StatusCode);
            throw new ApiException(
                "discord_identity_failed",
                "Discord identity could not be loaded.",
                StatusCodes.Status502BadGateway);
        }

        var profile = await identityResponse.Content.ReadFromJsonAsync<DiscordUserResponse>(cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new ApiException(
                "discord_invalid_response",
                "Discord returned an invalid identity response.",
                StatusCodes.Status502BadGateway);
        }

        var displayName = string.IsNullOrWhiteSpace(profile.GlobalName) ? profile.Username : profile.GlobalName;
        var avatarUrl = string.IsNullOrWhiteSpace(profile.Avatar)
            ? null
            : $"https://cdn.discordapp.com/avatars/{profile.Id}/{profile.Avatar}.png?size=128";

        return new(profile.Id, profile.Username, displayName, avatarUrl);
    }

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Warning,
        Message = "Discord {Operation} request failed with status {StatusCode}")]
    private static partial void LogDiscordFailure(ILogger logger, string operation, int statusCode);

    private sealed record DiscordTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record DiscordUserResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("global_name")] string? GlobalName,
        [property: JsonPropertyName("avatar")] string? Avatar);
}
