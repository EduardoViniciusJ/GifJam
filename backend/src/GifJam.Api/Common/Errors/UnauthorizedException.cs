namespace GifJam.Api.Common.Errors;

public sealed class UnauthorizedException(string code, string message)
    : AppException(code, message, StatusCodes.Status401Unauthorized);
