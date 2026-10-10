using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Ipc.Client;

/// <summary>
/// Describes a non-success application response returned by the daemon.
/// </summary>
public sealed class UfwIpcError(int statusCode, string? responseMessage, ModelValidationError[]? validationErrors = null, string? code = null)
{
    public int StatusCode { get; } = statusCode;

    public string? ResponseMessage { get; } = responseMessage;

    public ModelValidationError[]? ValidationErrors { get; } = validationErrors;

    public string? Code { get; } = code;
}
