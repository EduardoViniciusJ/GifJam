namespace GifJam.Api.Common.Errors;

public class ApiException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
