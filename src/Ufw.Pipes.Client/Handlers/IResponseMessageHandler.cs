using Ufw.Pipes.Shared.Handlers;
using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Pipes.Client.Handlers;

internal interface IResponseMessageHandler : IMessageHandler, IPipelineHandler
{
    ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken) where TResult : IEquatable<TResult>;
}
