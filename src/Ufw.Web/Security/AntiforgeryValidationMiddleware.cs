using Microsoft.AspNetCore.Antiforgery;

namespace Ufw.Web.Security;

internal sealed partial class AntiforgeryValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery, ILogger<AntiforgeryValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireAntiforgeryValidationAttribute>() is null)
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException exception)
        {
            LogInvalidToken(logger, exception.Message, exception);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await next(context);
    }

    [LoggerMessage(1, LogLevel.Information, "Antiforgery token validation failed. {Message}", EventName = "AntiforgeryTokenInvalid")]
    private static partial void LogInvalidToken(ILogger logger, string message, Exception exception);
}
