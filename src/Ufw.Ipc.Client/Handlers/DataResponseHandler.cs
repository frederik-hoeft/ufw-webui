using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

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
