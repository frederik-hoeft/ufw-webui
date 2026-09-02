using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class BadRequestResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -2;

    public bool CanHandle(IResponseMessage message) =>
        message.StatusCode == 400;

    public async ValueTask<TResult> TryHandleAsync<TResult>(IResponseMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        if (message.PayloadType == ApplicationPayloadTypes.VALIDATION_ERROR)
        {
            ModelValidationErrorResponse? validationErrorResponse =
                await message.Payload.ReadAsync<ModelValidationErrorResponse>(cancellationToken);
            if (validationErrorResponse?.Errors is not { Length: > 0 })
            {
                throw new InvalidDataException(
                    $"Response '{message.StatusCode}' declared payloadType '{message.PayloadType}' but did not contain validation errors.");
            }

            throw new UfwIpcException(message.StatusCode, validationErrorResponse.Message, validationErrorResponse.Errors);
        }

        if (message.PayloadType == ApplicationPayloadTypes.ERROR)
        {
            ErrorResponse? errorResponse = await message.Payload.ReadAsync<ErrorResponse>(cancellationToken);
            _ = errorResponse ?? throw new InvalidDataException(
                $"Response '{message.StatusCode}' declared payloadType '{message.PayloadType}' but the body was empty.");
            throw new UfwIpcException(message.StatusCode, errorResponse.Message);
        }

        throw new InvalidDataException(
            $"Response '{message.StatusCode}' has unsupported payloadType '{message.PayloadType}' for a 400 response.");
    }
}
