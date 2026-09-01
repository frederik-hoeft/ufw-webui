using Ufw.Ipc.Shared.Pipelines;
using Ufw.Ipc.Shared.Serialization;

namespace Ufw.Ipc.Shared.Handlers;

public interface IMessageHandler : IPipelineHandler
{
    bool CanHandle(IResponseMessage message);
}
