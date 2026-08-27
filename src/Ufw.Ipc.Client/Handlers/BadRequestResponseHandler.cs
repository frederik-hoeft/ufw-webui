using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class BadRequestResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -2;

    public bool CanHandle(IMessage message) => message.Id == "400";

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        ModelValidationErrorResponse? validationErrorResponse = await message.Payload.ReadAsync<ModelValidationErrorResponse>(cancellationToken);
        ModelValidationError[] modelValidationErrorArray = validationErrorResponse is not null
            ? validationErrorResponse.Errors
            : throw new InvalidDataException($"Failed to deserialize response body of message type '{message.Id}'");

        if (modelValidationErrorArray is { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"""
                Failed to perform request. Named pipe server returned status code {message.Id} '{validationErrorResponse.Message}': 
                    {string.Join("\n    ", modelValidationErrorArray.Select(static e => $"{e.PropertyName}: {e.ErrorMessage}"))}");
                """);
        }

        throw new InvalidOperationException($"Failed to perform request. Named pipe server returned status code {message.Id}: '{validationErrorResponse.Message}'");
    }
}
