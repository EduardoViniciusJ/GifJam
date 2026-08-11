namespace GifJam.Api.Common.Errors;

public sealed class BadRequestException(string code, string message)
    : AppException(code, message, StatusCodes.Status400BadRequest);
