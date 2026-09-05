using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Ufw.Client.Api;

namespace Ufw.Client.Errors;

internal sealed class ClientErrorMapper(ILogger<ClientErrorMapper> logger) : IClientErrorMapper
{
    public bool TryDescribe(Exception exception, out ClientError error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        ClientError? known = exception switch
        {
            ApiRequestException apiException => DescribeApiRequest(apiException),
            ApiProtocolException protocolException => DescribeProtocolError(protocolException),
            HttpRequestException => new(
                ClientErrorKind.Unavailable,
                "The management API is unavailable. Check the connection and try again.",
                Retryable: true),
            OperationCanceledException => new(
                ClientErrorKind.Canceled,
                "The operation was canceled before it completed.",
                Retryable: true),
            BrowserOperationException or JSException or JSDisconnectedException => new(
                ClientErrorKind.Browser,
                "The browser could not complete a required security operation. Try again in a supported browser.",
                Retryable: true),
            ArgumentException argumentException => new(
                ClientErrorKind.RequestRejected,
                argumentException.Message,
                Retryable: false),
            _ => null,
        };

        if (known is null)
        {
            error = null!;
            return false;
        }

        error = known;
        return true;
    }

    public ClientError Describe(Exception exception)
    {
        if (TryDescribe(exception, out ClientError error))
        {
            return error;
        }

        logger.LogError(exception, "An unexpected client error occurred.");
        return new(
            ClientErrorKind.Unexpected,
            "An unexpected error occurred. Refresh the application and try again.",
            Retryable: true);
    }

    private ClientError DescribeApiRequest(ApiRequestException exception)
    {
        int statusCode = (int)exception.StatusCode;
        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(
                ClientErrorKind.Unauthorized,
                "Your session is no longer valid. Sign in again.",
                Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return new(
                ClientErrorKind.Forbidden,
                "You do not have permission to perform this operation.",
                Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return new(ClientErrorKind.Conflict, exception.Message, Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new(
                ClientErrorKind.RequestRejected,
                "The requested resource no longer exists. Refresh and try again.",
                Retryable: true);
        }

        if (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            return new(ClientErrorKind.RequestRejected, exception.Message, Retryable: false);
        }

        if (exception.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || statusCode >= 500)
        {
            return new(
                ClientErrorKind.Unavailable,
                "The management API could not complete the request. Try again.",
                Retryable: true);
        }

        return new(
            ClientErrorKind.RequestRejected,
            "The management API rejected the request.",
            Retryable: false);
    }

    private ClientError DescribeProtocolError(ApiProtocolException exception)
    {
        logger.LogWarning(exception, "The management API returned an invalid or incompatible response.");
        return new(
            ClientErrorKind.Protocol,
            "The management API returned a response this client could not understand. Check that the client and server versions match.",
            Retryable: false);
    }
}
