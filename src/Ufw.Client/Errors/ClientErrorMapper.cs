using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Net;
using Ufw.Client.Api;
using Ufw.Client.Localization;

namespace Ufw.Client.Errors;

internal sealed partial class ClientErrorMapper(ILogger<ClientErrorMapper> logger, IStringLocalizer<ErrorsStrings> errorsText) : IClientErrorMapper
{
    public bool TryDescribe(Exception exception, out ClientError clientError)
    {
        ArgumentNullException.ThrowIfNull(exception);

        ClientError? known = exception switch
        {
            ApiRequestException apiException => DescribeApiRequest(apiException),
            ApiProtocolException protocolException => DescribeProtocolError(protocolException),
            HttpRequestException => new(ClientErrorKind.Unavailable, errorsText["ApiUnavailable"], Retryable: true),
            OperationCanceledException => new(ClientErrorKind.Canceled, errorsText["OperationCanceled"], Retryable: true),
            BrowserOperationException or JSException or JSDisconnectedException => new(ClientErrorKind.Browser, errorsText["BrowserSecurityOperationFailed"], Retryable: true),
            ArgumentException => new(ClientErrorKind.RequestRejected, errorsText["ClientValidationRejected"], Retryable: false),
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
            LogKnownFailure(exception);
            return clientError;
        }

        string reference = GetOrCreateDiagnosticReference(exception);
        LogUnexpectedClientError(logger, reference, exception);
        return new(ClientErrorKind.Unexpected, errorsText["Unexpected"], Retryable: true, DiagnosticReference: reference);
    }

    private ClientError DescribeApiRequest(ApiRequestException exception)
    {
        int statusCode = (int)exception.StatusCode;
        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new(ClientErrorKind.Unauthorized, errorsText["SessionInvalid"], Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            return new(ClientErrorKind.Forbidden, errorsText["Forbidden"], Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.Conflict)
        {
            return new(ClientErrorKind.Conflict, exception.Message, Retryable: false);
        }

        if (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return new(ClientErrorKind.RequestRejected, errorsText["ResourceMissing"], Retryable: true);
        }

        if (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            return new(ClientErrorKind.RequestRejected, exception.Message, Retryable: false);
        }

        if (exception.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || statusCode >= 500)
        {
            return new(ClientErrorKind.Unavailable, errorsText["ApiCouldNotComplete"], Retryable: true);
        }

        return new(ClientErrorKind.RequestRejected, errorsText["ApiRejected"], Retryable: false);
    }

    [LoggerMessage(LogLevel.Error, "Unexpected client error {DiagnosticReference}.")]
    private static partial void LogUnexpectedClientError(ILogger logger, string diagnosticReference, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Management API request {Method} {RequestUri} failed with HTTP {StatusCode}.")]
    private static partial void LogApiRequestFailure(ILogger logger, string method, string requestUri, int statusCode, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Management API transport failed.")]
    private static partial void LogApiTransportFailure(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "The browser could not complete a required client operation.")]
    private static partial void LogBrowserOperationFailure(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "The management API returned an invalid or incompatible response.")]
    private static partial void LogProtocolError(ILogger logger, Exception exception);

    private static string GetOrCreateDiagnosticReference(Exception exception)
    {
        const string DIAGNOSTIC_REFERENCE_KEY = "Ufw.Client.DiagnosticReference";
        if (exception.Data[DIAGNOSTIC_REFERENCE_KEY] is string existing && !string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        string reference = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        exception.Data[DIAGNOSTIC_REFERENCE_KEY] = reference;
        return reference;
    }

    private void LogKnownFailure(Exception exception)
    {
        switch (exception)
        {
            case ApiRequestException apiException:
                LogApiRequestFailure(logger, apiException.Method?.Method ?? "?", apiException.RequestUri?.PathAndQuery ?? "?", (int)apiException.StatusCode, apiException);
                break;
            case ApiProtocolException protocolException:
                LogProtocolError(logger, protocolException);
                break;
            case HttpRequestException httpException:
                LogApiTransportFailure(logger, httpException);
                break;
            case BrowserOperationException or JSException or JSDisconnectedException:
                LogBrowserOperationFailure(logger, exception);
                break;
        }
    }

    private ClientError DescribeProtocolError(ApiProtocolException exception)
    {
        return new(ClientErrorKind.Protocol, errorsText["ProtocolMismatch"], Retryable: false);
    }
}
