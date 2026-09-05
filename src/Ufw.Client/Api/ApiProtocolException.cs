namespace Ufw.Client.Api;

public sealed class ApiProtocolException(string message, Exception? innerException = null)
    : Exception(message, innerException);
