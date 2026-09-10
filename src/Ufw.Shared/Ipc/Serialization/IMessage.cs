using Ufw.Shared.Ipc.Protocol;

namespace Ufw.Shared.Ipc.Serialization;

public interface IMessage : IDisposable, IAsyncDisposable
{
    ApplicationMessageKind Kind { get; }

    int ProtocolVersion { get; }

    string PayloadType { get; }

    IMessageBlob Payload { get; }
}
