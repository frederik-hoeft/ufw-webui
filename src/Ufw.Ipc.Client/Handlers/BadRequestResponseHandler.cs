using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class BadRequestResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -2;

    public bool CanHandle(IMessage message) =>
        message.Kind == ApplicationMessageKind.Response && message.StatusCode == 400;

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        if (message.PayloadType == ApplicationPayloadTypes.ValidationError)
        {
            ModelValidationErrorResponse? validationErrorResponse =
                await message.Payload.ReadAsync<ModelValidationErrorResponse>(cancellationToken);
            if (validationErrorResponse?.Errors is not { Length: > 0 })
            {
                throw new InvalidDataException(
                    $"Response '{message.Id}' declared payloadType '{message.PayloadType}' but did not contain validation errors.");
            }

            throw new InvalidOperationException(
                $"""
                Failed to perform request. Server returned status code {message.Id} '{validationErrorResponse.Message}':
                    {string.Join("\n    ", validationErrorResponse.Errors.Select(static e => $"{e.PropertyName}: {e.ErrorMessage}"))}
                """);
        }

        if (message.PayloadType == ApplicationPayloadTypes.Error)
        {
            ErrorResponse? errorResponse = await message.Payload.ReadAsync<ErrorResponse>(cancellationToken);
            _ = errorResponse ?? throw new InvalidDataException(
                $"Response '{message.Id}' declared payloadType '{message.PayloadType}' but the body was empty.");
            throw new InvalidOperationException(
                $"Failed to perform request. Server returned status code {message.Id}: '{errorResponse.Message}'");
        }

        throw new InvalidDataException(
            $"Response '{message.Id}' has unsupported payloadType '{message.PayloadType}' for a 400 response.");
    }
}
