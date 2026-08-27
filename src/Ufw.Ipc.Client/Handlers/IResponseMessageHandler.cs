using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal interface IResponseMessageHandler : IMessageHandler, IPipelineHandler
{
    ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken) where TResult : IEquatable<TResult>;
}
