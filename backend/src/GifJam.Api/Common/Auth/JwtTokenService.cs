using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GifJam.Api.Common.Time;
using GifJam.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GifJam.Api.Common.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock)
{
    public TokenResult Create(User user)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddHours(options.Value.LifetimeHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim("discord_id", user.DiscordId),
            new Claim(JwtRegisteredClaimNames.Sid, Guid.CreateVersion7().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Value.Issuer,
            audience: options.Value.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.UniqueName
    };
}

public sealed record TokenResult(string AccessToken, DateTimeOffset ExpiresAt);
