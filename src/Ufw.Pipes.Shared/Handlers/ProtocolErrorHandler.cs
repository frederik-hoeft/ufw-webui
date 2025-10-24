using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Pipes.Shared.Handlers;

public abstract class ProtocolErrorHandler : IMessageHandler, IPipelineHandler
{
    public int Priority => int.MaxValue;

    public bool CanHandle(IMessage message) => true;

    protected static string ProtocolErrorMessage(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message, nameof(message));
        return $"No handler has been registered that can interpret messages of type '{message.Id}'.";
    }

    protected static Exception ProtocolError(IMessage message) => new InvalidDataException(ProtocolErrorMessage(message));
}
