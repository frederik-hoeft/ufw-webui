using Wkg.AspNetCore.ErrorHandling;

namespace Ufw.Web.Services.ErrorHandling;

internal sealed partial class ApplicationErrorSentry(ILogger<ApplicationErrorSentry> logger) : DefaultErrorSentry
{
    protected override void OnError(Exception exception) => LogUnhandledException(logger, exception);

    [LoggerMessage(1, LogLevel.Error, "Unhandled exception in an application request scope.")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);
}
