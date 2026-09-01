using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Shared.Handlers;

public abstract class ProtocolErrorHandler : IMessageHandler, IPipelineHandler
{
    public int Priority => int.MaxValue;

    public bool CanHandle(IResponseMessage message) => true;

    protected static string ProtocolErrorMessage(IResponseMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        return $"No handler has been registered that can interpret response status '{message.StatusCode}', payloadType '{message.PayloadType}'.";
    }

    protected static Exception ProtocolError(IResponseMessage message) => new InvalidDataException(ProtocolErrorMessage(message));
}
