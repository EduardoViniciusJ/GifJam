using System.Security.Claims;
using GifJam.Api.Common.Errors;

namespace GifJam.Api.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
        {
            throw new ApiException("invalid_identity", "The authenticated identity is invalid.", StatusCodes.Status401Unauthorized);
        }

        return userId;
    }
}
