using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class ErrorResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -1;

    public bool CanHandle(IResponseMessage message) =>
        message.StatusCode is not 200;

    public async ValueTask<TResult> TryHandleAsync<TResult>(IResponseMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        if (message.PayloadType != ApplicationPayloadTypes.Error)
        {
            throw new InvalidDataException(
                $"Response '{message.StatusCode}' has unsupported payloadType '{message.PayloadType}' for status {message.StatusCode}.");
        }

        ErrorResponse? errorResponse = await message.Payload.ReadAsync<ErrorResponse>(cancellationToken);
        _ = errorResponse ?? throw new InvalidDataException($"Failed to deserialize response body of message type '{message.StatusCode}'");
        throw new InvalidOperationException($"Failed to perform request. Server returned status code {message.StatusCode}: '{errorResponse.Message}'");
    }
}
