namespace GifJam.Api.Common.Errors;

public sealed class NotFoundException(string code, string message)
    : AppException(code, message, StatusCodes.Status404NotFound);
