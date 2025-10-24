namespace Ufw.Pipes.Shared.Serialization;

public interface IMessage : IDisposable, IAsyncDisposable
{
    string Id { get; }

    string? Method { get; }

    IMessageBlob Payload { get; }
}
