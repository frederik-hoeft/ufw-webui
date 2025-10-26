using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Ufw.Pipes.Shared;
using Ufw.Pipes.Shared.Handlers;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Pipes.Client.Handlers;

internal sealed class DataResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    private static readonly OkResponse s_okResponse = new();

    public int Priority => 0;

    public bool CanHandle(IMessage message) => message.Id == "200";

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        if (typeof(TResult) == typeof(OkResponse))
        {
            await message.Payload.TryReadAsync(Timeout.InfiniteTimeSpan, cancellationToken);
            OkResponse okResponse = s_okResponse;
            return Unsafe.As<OkResponse, TResult>(ref okResponse);
        }

        TResult? result = await message.Payload.ReadAsync<TResult>(cancellationToken);
        return result ?? throw new SerializationException($"Unable to deserialize payload of pipe message '{message.Id}' to type {typeof(TResult)}.");
    }
}
