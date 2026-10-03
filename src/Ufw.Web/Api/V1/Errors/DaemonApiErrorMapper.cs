using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Api.V1.Errors;

internal sealed class DaemonApiErrorMapper : IDaemonApiErrorMapper
{
    public DaemonApiError MapProxyFailure(UfwIpcError daemonError)
    {
        ArgumentNullException.ThrowIfNull(daemonError);

        if (daemonError.ValidationErrors is { Length: > 0 })
        {
            ValidationProblemDetails problem = new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = daemonError.ResponseMessage ?? "One or more validation errors occurred.",
            };
            foreach (ModelValidationError error in daemonError.ValidationErrors)
            {
                problem.Errors[error.PropertyName] = [error.ErrorMessage];
            }
            return new DaemonApiError(StatusCodes.Status400BadRequest, problem);
        }

        int statusCode = daemonError.StatusCode is >= 400 and <= 599 ? daemonError.StatusCode : StatusCodes.Status502BadGateway;
        return new DaemonApiError(statusCode, CreateProblem(statusCode, daemonError.ResponseMessage));
    }

    public DaemonApiError MapUnavailable(UfwIpcError daemonError)
    {
        ArgumentNullException.ThrowIfNull(daemonError);
        return new DaemonApiError(StatusCodes.Status502BadGateway, CreateProblem(StatusCodes.Status502BadGateway, daemonError.ResponseMessage));
    }

    public DaemonApiError MapInvalidResponse(DaemonInvalidResponseException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new DaemonApiError(StatusCodes.Status502BadGateway, CreateProblem(StatusCodes.Status502BadGateway, exception.Message));
    }

    private static ProblemDetails CreateProblem(int statusCode, string? detail) => new()
    {
        Status = statusCode,
        Detail = detail,
    };
}
