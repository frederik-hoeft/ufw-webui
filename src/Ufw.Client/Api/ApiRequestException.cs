using System.Net;

namespace Ufw.Client.Api;

public sealed class ApiRequestException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
