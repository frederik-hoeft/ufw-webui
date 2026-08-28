using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Ufw.Ipc.Shared.Handlers;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Client.Handlers;

internal sealed class DataResponseHandler : IResponseMessageHandler, IMessageHandler, IPipelineHandler
{
    private static readonly OkResponse s_okResponse = new();

    public int Priority => 0;

    public bool CanHandle(IMessage message) =>
        message.Kind == ApplicationMessageKind.Response && message.StatusCode == 200;

    public async ValueTask<TResult> TryHandleAsync<TResult>(IMessage message, CancellationToken cancellationToken)
        where TResult : IEquatable<TResult>
    {
        if (message.PayloadType == ApplicationPayloadTypes.Empty)
        {
            if (typeof(TResult) != typeof(OkResponse))
            {
                throw new SerializationException(
                    $"Response '{message.Id}' has payloadType '{message.PayloadType}' but {typeof(TResult).Name} was requested.");
            }

            OkResponse okResponse = s_okResponse;
            return Unsafe.As<OkResponse, TResult>(ref okResponse);
        }

        if (message.PayloadType != ApplicationPayloadTypes.Data)
        {
            throw new SerializationException(
                $"Response '{message.Id}' has unexpected payloadType '{message.PayloadType}' for a 200 response.");
        }

        TResult? result = await message.Payload.ReadAsync<TResult>(cancellationToken);
        return result ?? throw new SerializationException($"Unable to deserialize payload of response '{message.Id}' to type {typeof(TResult)}.");
    }
}
