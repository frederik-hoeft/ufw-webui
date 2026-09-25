using System.Net;

namespace Ufw.Web.Client.Api;

public sealed class ApiRequestException(HttpStatusCode statusCode, string message, HttpMethod? method = null, Uri? requestUri = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public HttpMethod? Method { get; } = method;

    public Uri? RequestUri { get; } = requestUri;
}
