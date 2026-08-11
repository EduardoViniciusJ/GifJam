namespace GifJam.Api.Common.Errors;

public sealed class ForbiddenException(string code, string message)
    : AppException(code, message, StatusCodes.Status403Forbidden);
