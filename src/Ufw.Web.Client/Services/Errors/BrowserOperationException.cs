namespace Ufw.Web.Client.Services.Errors;

internal sealed class BrowserOperationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
