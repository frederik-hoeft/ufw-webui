using Ufw.Shared.Ipc.Protocol;

namespace Ufw.Shared.Ipc.Serialization;

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
