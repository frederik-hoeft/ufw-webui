using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class ResponseProtocolErrorHandler : ProtocolErrorHandler, IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    public ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken) where TResult : IEquatable<TResult> => 
        throw ProtocolError(message);
}
