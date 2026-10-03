using System.Net;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Client.Api;

public sealed class ApiRequestException(
    HttpStatusCode statusCode,
    string message,
    HttpMethod? method = null,
    Uri? requestUri = null,
    IReadOnlyList<ApiValidationError>? validationErrors = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public HttpMethod? Method { get; } = method;

    public Uri? RequestUri { get; } = requestUri;

    public IReadOnlyList<ApiValidationError> ValidationErrors { get; } = validationErrors ?? [];
}
