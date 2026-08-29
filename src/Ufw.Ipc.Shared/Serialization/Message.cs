using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

internal abstract class MessageBase(
    ApplicationMessageKind kind,
    int protocolVersion,
    string payloadType,
    IMessageBlob payload) : IMessage
{
    private bool _disposedValue;

    public ApplicationMessageKind Kind
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return kind;
        }
    }

    public int ProtocolVersion
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return protocolVersion;
        }
    }

    public string PayloadType
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return payloadType;
        }
    }

    public IMessageBlob Payload
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return payload;
        }
    }

    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposedValue, this);

    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }

        payload.Dispose();
        _disposedValue = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposedValue)
        {
            return;
        }

        await payload.DisposeAsync();
        _disposedValue = true;
    }
}

internal sealed class RequestMessage(
    int protocolVersion,
    string method,
    string route,
    string payloadType,
    IMessageBlob payload)
    : MessageBase(ApplicationMessageKind.Request, protocolVersion, payloadType, payload), IRequestMessage
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

internal sealed class ResponseMessage(
    int protocolVersion,
    int statusCode,
    string payloadType,
    IMessageBlob payload)
    : MessageBase(ApplicationMessageKind.Response, protocolVersion, payloadType, payload), IResponseMessage
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
