namespace GifJam.Api.Integrations.Giphy;

public sealed class GiphyCursorException(string message, Exception? innerException = null)
    : Exception(message, innerException);
