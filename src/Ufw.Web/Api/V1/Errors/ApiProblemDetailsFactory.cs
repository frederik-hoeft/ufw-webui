using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Api.V1.Errors;

internal static class ApiProblemDetailsFactory
{
    private const string DEFAULT_VALIDATION_TITLE = "One or more validation errors occurred.";

    public static ProblemDetails Create(int statusCode, string? title = null, string? detail = null) => new()
    {
        Status = statusCode,
        Title = title,
        Detail = detail,
    };

    public static ProblemDetails CreateValidation(IReadOnlyList<ApiValidationError> validationErrors, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);

        ProblemDetails problem = Create(StatusCodes.Status400BadRequest, title ?? DEFAULT_VALIDATION_TITLE);
        problem.Extensions[ApiProblemDetails.VALIDATION_ERRORS_PROPERTY] = validationErrors;
        return problem;
    }

    public static ProblemDetails CreateValidation(ModelStateDictionary modelState, string? title = null) =>
        CreateValidation(modelState, structuredErrors: null, title);

    public static ProblemDetails CreateValidation(ModelStateDictionary modelState, IReadOnlyList<ApiValidationError>? structuredErrors, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        ApiValidationError[] errors = modelState
            .Where(static entry => entry.Value is { Errors.Count: > 0 })
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
            {
                string message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage;
                string? code = structuredErrors?.FirstOrDefault(candidate =>
                    string.Equals(candidate.PropertyName, entry.Key, StringComparison.Ordinal)
                    && string.Equals(candidate.Message, message, StringComparison.Ordinal))?.Code;
                return new ApiValidationError(entry.Key, code, message);
            }))
            .ToArray();
        return CreateValidation(errors, title);
    }
}
