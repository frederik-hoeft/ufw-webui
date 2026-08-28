using Ufw.Ipc.Shared.Protocol;

namespace Ufw.Ipc.Shared.Serialization;

public interface IMessage : IDisposable, IAsyncDisposable
{
    ApplicationMessageKind Kind { get; }

    int ProtocolVersion { get; }

    /// <summary>
    /// Route for requests, decimal status for responses. Kept so routing and existing
    /// call sites do not need a conceptual change.
    /// </summary>
    string Id { get; }

    string? Method { get; }

    string? Route { get; }

    int? StatusCode { get; }

    string PayloadType { get; }

    IMessageBlob Payload { get; }
}
