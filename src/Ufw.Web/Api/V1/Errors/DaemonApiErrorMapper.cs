using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Web.Api.V1.Errors;

internal sealed class DaemonApiErrorMapper : IDaemonApiErrorMapper
{
    public DaemonApiError MapProxyFailure(UfwIpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.ValidationErrors is { Length: > 0 })
        {
            ValidationProblemDetails problem = new()
            {
                Status = StatusCodes.Status400BadRequest,
                Title = exception.ResponseMessage ?? "One or more validation errors occurred.",
            };
            foreach (ModelValidationError error in exception.ValidationErrors)
            {
                problem.Errors[error.PropertyName] = [error.ErrorMessage];
            }
            return new DaemonApiError(StatusCodes.Status400BadRequest, problem);
        }

        int statusCode = exception.StatusCode is >= 400 and <= 599 ? exception.StatusCode : StatusCodes.Status502BadGateway;
        return new DaemonApiError(statusCode, CreateProblem(statusCode, exception.ResponseMessage));
    }

    public DaemonApiError MapUnavailable(UfwIpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new DaemonApiError(StatusCodes.Status502BadGateway, CreateProblem(StatusCodes.Status502BadGateway, exception.ResponseMessage));
    }

    public DaemonApiError MapInvalidResponse(InvalidDataException exception)
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
