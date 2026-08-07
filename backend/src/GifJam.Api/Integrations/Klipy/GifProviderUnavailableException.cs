namespace GifJam.Api.Integrations.Klipy;

public sealed class GifProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
