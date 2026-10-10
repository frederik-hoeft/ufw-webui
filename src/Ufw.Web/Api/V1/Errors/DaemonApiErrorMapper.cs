using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Api.V1.Errors;

internal sealed class DaemonApiErrorMapper : IDaemonApiErrorMapper
{
    public DaemonApiError MapProxyFailure(UfwIpcError daemonError)
    {
        ArgumentNullException.ThrowIfNull(daemonError);

        if (daemonError.ValidationErrors is { Length: > 0 })
        {
            ApiValidationError[] validationErrors = [.. daemonError.ValidationErrors.Select(static error => new ApiValidationError(error.PropertyName, error.Code, error.ErrorMessage))];
            ProblemDetails validationProblem = ApiProblemDetailsFactory.CreateValidation(validationErrors, daemonError.ResponseMessage);
            return new DaemonApiError(StatusCodes.Status400BadRequest, validationProblem);
        }

        int statusCode = daemonError.StatusCode is >= 400 and <= 599 ? daemonError.StatusCode : StatusCodes.Status502BadGateway;
        ProblemDetails problem = ApiProblemDetailsFactory.Create(statusCode, detail: daemonError.ResponseMessage);
        if (!string.IsNullOrWhiteSpace(daemonError.Code))
        {
            problem.Extensions["code"] = daemonError.Code;
        }
        return new DaemonApiError(statusCode, problem);
    }

    public DaemonApiError MapUnavailable(UfwIpcError daemonError)
    {
        ArgumentNullException.ThrowIfNull(daemonError);
        return new DaemonApiError(
            StatusCodes.Status502BadGateway,
            ApiProblemDetailsFactory.Create(StatusCodes.Status502BadGateway, detail: daemonError.ResponseMessage));
    }

    public DaemonApiError MapInvalidResponse(DaemonInvalidResponseException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new DaemonApiError(
            StatusCodes.Status502BadGateway,
            ApiProblemDetailsFactory.Create(StatusCodes.Status502BadGateway, detail: exception.Message));
    }
}
