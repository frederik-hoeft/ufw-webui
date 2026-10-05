using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ufw.Ipc.Client;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Api.V1.Errors;

internal sealed class DaemonApiExceptionFilter(IDaemonApiErrorMapper errors) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        DaemonApiError? error = context.Exception switch
        {
            UfwIpcException exception => errors.MapProxyFailure(exception.Error),
            DaemonUnavailableException exception => errors.MapUnavailable(exception.Error),
            DaemonInvalidResponseException exception => errors.MapInvalidResponse(exception),
            _ => null,
        };
        if (error is null)
        {
            return;
        }

        context.Result = new ObjectResult(error.Problem) { StatusCode = error.StatusCode };
        context.ExceptionHandled = true;
    }
}
