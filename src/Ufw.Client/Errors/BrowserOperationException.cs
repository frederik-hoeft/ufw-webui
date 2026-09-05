namespace Ufw.Client.Errors;

public sealed class BrowserOperationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
