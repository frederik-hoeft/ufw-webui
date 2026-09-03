using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

internal abstract class MessageBase(ApplicationMessageKind kind, int protocolVersion, string payloadType, IMessageBlob payload) : IMessage
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
