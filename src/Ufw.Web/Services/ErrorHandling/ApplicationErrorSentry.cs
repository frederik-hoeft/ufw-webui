using Wkg.AspNetCore.ErrorHandling;

namespace Ufw.Web.Services.ErrorHandling;

internal sealed class ApplicationErrorSentry(ILogger<ApplicationErrorSentry> logger) : DefaultErrorSentry
{
    protected override void OnError(Exception exception) =>
        logger.LogError(exception, "Unhandled exception in an application request scope.");
}
