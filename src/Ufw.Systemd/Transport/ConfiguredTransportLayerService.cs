using Ufw.Shared.Ipc.Transport;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Tcp;

namespace Ufw.Systemd.Transport;

internal sealed class ConfiguredTransportLayerService : ITransportLayerService
{
    private readonly ITransportLayerService _selectedTransport;

    public ConfiguredTransportLayerService(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        Type transportServiceType = configuration.Settings.Transport.Type switch
        {
            TransportType.Pipe => typeof(IPipeTransportLayerService),
            TransportType.Tcp => typeof(ITcpTransportLayerService),
            TransportType type => throw new InvalidOperationException($"Unsupported transport type '{type}'."),
        };

        _selectedTransport = serviceProvider.GetService(transportServiceType) as ITransportLayerService
            ?? throw new InvalidOperationException($"Selected transport service '{transportServiceType.Name}' is not registered.");
    }

    public Task<ITransportLayerConnection> ServeAsync(CancellationToken cancellationToken) =>
        _selectedTransport.ServeAsync(cancellationToken);
}
