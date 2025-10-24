namespace Ufw.Pipes.Shared.Serialization;

internal sealed class Message(string id, string? method, IMessageBlob payload) : IMessage, IDisposable, IAsyncDisposable
{
    private bool _disposedValue;

    public string Id
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return id;
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

    public string? Method
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return method;
        }
    }

    public void Dispose()
    {
        Payload.Dispose();
        _disposedValue = true;
    }

    public async ValueTask DisposeAsync()
    {
        await Payload.DisposeAsync().NoCapture();
        _disposedValue = true;
    }
}
