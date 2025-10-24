using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Handlers;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Pipes.Client.Handlers;

internal sealed class BadRequestResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -2;

    public bool CanHandle(IMessage message) => message.Id == "400";

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        ModelValidationErrorResponse? validationErrorResponse = await message.Payload.ReadAsync<ModelValidationErrorResponse>(cancellationToken).NoCapture();
        ModelValidationError[] modelValidationErrorArray = validationErrorResponse is not null 
            ? validationErrorResponse.Errors 
            : throw new InvalidDataException($"Failed to deserialize response body of message type '{message.Id}'");

        if (modelValidationErrorArray is { Length: > 0})
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
