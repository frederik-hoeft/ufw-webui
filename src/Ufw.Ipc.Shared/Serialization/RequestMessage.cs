using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

internal sealed class RequestMessage
(
    int protocolVersion,
    string method,
    string route,
    string payloadType,
    IMessageBlob payload
) : MessageBase(ApplicationMessageKind.Request, protocolVersion, payloadType, payload), IRequestMessage
{
    public string Method
    {
        get
        {
            ThrowIfDisposed();
            return method;
        }
    }

    public string Route
    {
        get
        {
            ThrowIfDisposed();
            return route;
        }
    }
}
