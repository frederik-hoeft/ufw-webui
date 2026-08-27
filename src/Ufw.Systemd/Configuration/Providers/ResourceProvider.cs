using System.Collections.Immutable;
using Ufw.Ipc.Shared.Pipelines;

namespace Ufw.Systemd.Configuration.Providers;

internal sealed class ResourceProvider(IEnumerable<IResourceProviderStrategy> strategies) : IResourceProvider
{
    private readonly ImmutableArray<IResourceProviderStrategy> _strategies = strategies.CreatePipeline();

    public IResourceProviderStrategy? PreferredStrategy { get; set; }

    public Stream? OpenRead(string resourceName)
    {
        if (PreferredStrategy is not null)
        {
            return PreferredStrategy.OpenRead(resourceName);
        }
        foreach (IResourceProviderStrategy strategy in _strategies)
        {
            Stream? stream = strategy.OpenRead(resourceName);
            if (stream is not null)
            {
                PreferredStrategy = strategy;
                return stream;
            }
        }
        return null;
    }
}
