namespace GifJam.Api.Common.Errors;

public sealed class ConflictException(string code, string message)
    : AppException(code, message, StatusCodes.Status409Conflict);
