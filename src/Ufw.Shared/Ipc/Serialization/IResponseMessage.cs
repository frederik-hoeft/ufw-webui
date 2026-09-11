namespace Ufw.Shared.Ipc.Serialization;

public interface IResponseMessage : IMessage
{
    int StatusCode { get; }
}
