using Ufw.Shared.Ipc.Pipelines;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Shared.Ipc.Handlers;

public interface IMessageHandler : IPipelineHandler
{
    bool CanHandle(IResponseMessage message);
}
