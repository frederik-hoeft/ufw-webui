namespace Ufw.Shared.Ipc.Serialization;

public interface IRequestMessage : IMessage
{
    string Method { get; }

    string Route { get; }
}
