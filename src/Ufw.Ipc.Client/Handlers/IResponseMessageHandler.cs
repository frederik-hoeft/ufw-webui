using Ufw.Shared.Ipc.Handlers;
using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal interface IResponseMessageHandler : IMessageHandler, IPipelineHandler
{
    ValueTask<TResult> TryHandleAsync<TResult>(IResponseMessage message, CancellationToken cancellationToken) where TResult : IEquatable<TResult>;
}
