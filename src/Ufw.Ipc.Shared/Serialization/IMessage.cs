using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

public interface IMessage : IDisposable, IAsyncDisposable
{
    ApplicationMessageKind Kind { get; }

    int ProtocolVersion { get; }

    string PayloadType { get; }

    IMessageBlob Payload { get; }
}

public interface IRequestMessage : IMessage
{
    string Method { get; }

    string Route { get; }
}

public interface IResponseMessage : IMessage
{
    int StatusCode { get; }
}
