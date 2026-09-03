using System.Diagnostics.CodeAnalysis;
using Ufw.Ipc.Shared.Model.Responses;

namespace Ufw.Ipc.Client;

/// <summary>
/// Raised when the daemon returns a non-success application response.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Not needed for this exception type")]
public sealed class UfwIpcException(int statusCode, string? responseMessage, ModelValidationError[]? validationErrors = null)
    : InvalidOperationException(BuildMessage(statusCode, responseMessage, validationErrors))
{
    public int StatusCode { get; } = statusCode;

    public string? ResponseMessage { get; } = responseMessage;

    public ModelValidationError[]? ValidationErrors { get; } = validationErrors;

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
