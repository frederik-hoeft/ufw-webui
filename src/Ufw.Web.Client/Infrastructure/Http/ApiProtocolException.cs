namespace Ufw.Web.Client.Infrastructure.Http;

public sealed class ApiProtocolException(string message, Exception? innerException = null)
    : Exception(message, innerException);
