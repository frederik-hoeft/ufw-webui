using Ufw.Ipc.Shared.Pipelines;

namespace Ufw.Systemd.Configuration.Providers;

internal interface IResourceProviderStrategy : IPipelineHandler
{
    Stream? OpenRead(string resourceName);
}
