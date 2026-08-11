namespace GifJam.Api.Common.Errors;

/// <summary>
/// Base exception for expected application failures.
/// Public responses must use Code and StatusCode only; the exception message is for logs.
/// </summary>
public class AppException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
