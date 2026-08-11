namespace GifJam.Api.Common.Errors;

/// <summary>
/// Backwards-compatible application exception used by the current feature code.
/// New code can use the more specific exception types in this namespace.
/// </summary>
public class ApiException(string code, string message, int statusCode)
    : AppException(code, message, statusCode);
