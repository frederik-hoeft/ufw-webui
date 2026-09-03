using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

internal sealed class ResponseMessage
(
    int protocolVersion,
    int statusCode,
    string payloadType,
    IMessageBlob payload
) : MessageBase(ApplicationMessageKind.Response, protocolVersion, payloadType, payload), IResponseMessage
{
    public int StatusCode
    {
        get
        {
            ThrowIfDisposed();
            return statusCode;
        }
    }
}
