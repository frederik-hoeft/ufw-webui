using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Errors;

namespace Ufw.Web.Security;

internal sealed partial class AntiforgeryValidationMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    IProblemDetailsService problemDetails,
    ILogger<AntiforgeryValidationMiddleware> logger)
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
            ProblemDetails problem = ApiProblemDetailsFactory.Create(
                StatusCodes.Status400BadRequest,
                title: "Antiforgery validation failed",
                detail: "The antiforgery token is invalid or missing.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem,
            });
            return;
        }

        await next(context);
    }

    [LoggerMessage(1, LogLevel.Information, "Antiforgery token validation failed. {Message}", EventName = "AntiforgeryTokenInvalid")]
    private static partial void LogInvalidToken(ILogger logger, string message, Exception exception);
}
