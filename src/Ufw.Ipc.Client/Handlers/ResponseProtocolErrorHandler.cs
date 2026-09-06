using Ufw.Shared.Ipc.Handlers;
using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class ResponseProtocolErrorHandler : ProtocolErrorHandler, IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public ValueTask<TResult> TryHandleAsync<TResult>(IResponseMessage message, CancellationToken cancellationToken) where TResult : IEquatable<TResult> =>
        throw ProtocolError(message);
}
