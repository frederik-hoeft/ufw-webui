using Ufw.Pipes.Shared.Pipelines;
using Ufw.Pipes.Shared.Serialization;

namespace Ufw.Pipes.Shared.Handlers;

public interface IMessageHandler : IPipelineHandler
{
    bool CanHandle(IMessage message);
}
