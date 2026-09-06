using System.Net;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Ufw.Client.Api;
using Ufw.Client.Localization;

namespace Ufw.Client.Errors;

internal sealed partial class ClientErrorMapper(
    ILogger<ClientErrorMapper> logger,
    IStringLocalizer<ErrorsStrings> errorsText) : IClientErrorMapper
{
    public bool TryDescribe(Exception exception, out ClientError clientError)
    {
        ArgumentNullException.ThrowIfNull(exception);

        ClientError? known = exception switch
        {
            ApiRequestException apiException => DescribeApiRequest(apiException),
            ApiProtocolException protocolException => DescribeProtocolError(protocolException),
            HttpRequestException => new(
                ClientErrorKind.Unavailable,
                errorsText["ApiUnavailable"],
                Retryable: true),
            OperationCanceledException => new(
                ClientErrorKind.Canceled,
                errorsText["OperationCanceled"],
                Retryable: true),
            BrowserOperationException or JSException or JSDisconnectedException => new(
                ClientErrorKind.Browser,
                errorsText["BrowserSecurityOperationFailed"],
                Retryable: true),
            ArgumentException => new(
                ClientErrorKind.RequestRejected,
                errorsText["ClientValidationRejected"],
                Retryable: false),
            _ => null,
        };

        if (known is null)
        {
            clientError = null!;
            return false;
        }

        clientError = known;
        return true;
    }

    public ClientError Describe(Exception exception)
    {
        if (TryDescribe(exception, out ClientError clientError))
        {
            return clientError;
        }

        LogUnexpectedClientError(logger, exception);
        return new(
            ClientErrorKind.Unexpected,
            errorsText["Unexpected"],
            Retryable: true);
    }

    private ClientError DescribeApiRequest(ApiRequestException exception)
    {
        int statusCode = (int)exception.StatusCode;
        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(
                ClientErrorKind.Unauthorized,
                errorsText["SessionInvalid"],
                Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return new(
                ClientErrorKind.Forbidden,
                errorsText["Forbidden"],
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
                errorsText["ResourceMissing"],
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
                errorsText["ApiCouldNotComplete"],
                Retryable: true);
        }

        return new(
            ClientErrorKind.RequestRejected,
            errorsText["ApiRejected"],
            Retryable: false);
    }

    [LoggerMessage(LogLevel.Error, "An unexpected client error occurred.")]
    private static partial void LogUnexpectedClientError(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "The management API returned an invalid or incompatible response.")]
    private static partial void LogProtocolError(ILogger logger, Exception exception);

    private ClientError DescribeProtocolError(ApiProtocolException exception)
    {
        LogProtocolError(logger, exception);
        return new(
            ClientErrorKind.Protocol,
            errorsText["ProtocolMismatch"],
            Retryable: false);
    }
}
