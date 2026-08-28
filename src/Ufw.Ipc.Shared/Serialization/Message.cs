using System.Globalization;
using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

internal sealed class Message(
    ApplicationMessageKind kind,
    int protocolVersion,
    string? method,
    string? route,
    int? statusCode,
    string payloadType,
    IMessageBlob payload) : IMessage, IDisposable, IAsyncDisposable
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

    public string Id
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return kind == ApplicationMessageKind.Request
                ? route ?? string.Empty
                : (statusCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
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

    public string? Route
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return route;
        }
    }

    public int? StatusCode
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposedValue, this);
            return statusCode;
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

    public void Dispose()
    {
        Payload.Dispose();
        _disposedValue = true;
    }

    public async ValueTask DisposeAsync()
    {
        await Payload.DisposeAsync();
        _disposedValue = true;
    }
}
