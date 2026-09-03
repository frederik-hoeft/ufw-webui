using Ufw.Ipc.Shared.Model.Responses;

namespace Ufw.Ipc.Client;

/// <summary>
/// Raised when the daemon returns a non-success application response.
/// </summary>
public sealed class UfwIpcException : InvalidOperationException
{
    public UfwIpcException(int statusCode, string? responseMessage, ModelValidationError[]? validationErrors = null)
        : base(BuildMessage(statusCode, responseMessage, validationErrors))
    {
        StatusCode = statusCode;
        ResponseMessage = responseMessage;
        ValidationErrors = validationErrors;
    }

    public int StatusCode { get; }

    public string? ResponseMessage { get; }

    public ModelValidationError[]? ValidationErrors { get; }

    private static string BuildMessage(int statusCode, string? responseMessage, ModelValidationError[]? validationErrors)
    {
        if (validationErrors is { Length: > 0 })
        {
            return $"""
                Failed to perform request. Server returned status code {statusCode} '{responseMessage}':
                    {string.Join("\n    ", validationErrors.Select(static error => $"{error.PropertyName}: {error.ErrorMessage}"))}
                """;
        }

        return $"Failed to perform request. Server returned status code {statusCode}: '{responseMessage}'";
    }
}
