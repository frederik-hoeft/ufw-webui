namespace Ufw.Client.Errors;

internal sealed class BrowserOperationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
