using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Ipc.Client;

/// <summary>
/// Raised when a caller asserts success for a daemon request that returned a non-success application response.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Not needed for this exception type")]
public sealed class UfwIpcException : InvalidOperationException
{
    public UfwIpcException(int statusCode, string? responseMessage, ModelValidationError[]? validationErrors = null)
        : this(new UfwIpcError(statusCode, responseMessage, validationErrors))
    {
    }

    public UfwIpcException(UfwIpcError error)
        : base(BuildMessage(error))
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    public UfwIpcError Error { get; }

    public int StatusCode => Error.StatusCode;

    public string? ResponseMessage => Error.ResponseMessage;

    public ModelValidationError[]? ValidationErrors => Error.ValidationErrors;

    private static string BuildMessage(UfwIpcError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.ValidationErrors is { Length: > 0 })
        {
            return $"""
                Failed to perform request. Server returned status code {error.StatusCode} '{error.ResponseMessage}':
                    {string.Join("\n    ", error.ValidationErrors.Select(static validationError => $"{validationError.PropertyName}: {validationError.ErrorMessage}"))}
                """;
        }

        return $"Failed to perform request. Server returned status code {error.StatusCode}: '{error.ResponseMessage}'";
    }
}
