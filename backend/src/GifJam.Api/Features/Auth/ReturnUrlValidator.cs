using GifJam.Api.Common.Errors;

namespace GifJam.Api.Features.Auth;

public static class ReturnUrlValidator
{
    public static string Normalize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (returnUrl.Length > 512 ||
            !returnUrl.StartsWith('/') ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            returnUrl.Contains('\\') ||
            !Uri.TryCreate(returnUrl, UriKind.Relative, out _))
        {
            throw new ApiException(
                "invalid_return_url",
                "The return URL must be a local relative path.",
                StatusCodes.Status400BadRequest);
        }

        return returnUrl;
    }
}
