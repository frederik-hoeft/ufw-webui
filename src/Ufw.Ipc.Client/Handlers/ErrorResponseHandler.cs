using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class ErrorResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public int Priority => -1;

    public bool CanHandle(IMessage message) => message.Id != "200";

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        ErrorResponse? errorResponse = await message.Payload.ReadAsync<ErrorResponse>(cancellationToken);
        _ = errorResponse ?? throw new InvalidDataException($"Failed to deserialize response body of message type '{message.Id}'");
        throw new InvalidOperationException($"Failed to perform request. Named pipe server returned status code {message.Id}: '{errorResponse.Message}'");
    }
}
