using System.Net.NetworkInformation;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.NetworkInterfaces;

internal sealed class NetworkInterfaceSnapshotService(INetworkInterfaceProvider networkInterfaces, ILogger logger) : INetworkInterfaceSnapshotService
{
    private readonly ILogger<NetworkInterfaceSnapshotService> _logger = logger.Scoped<NetworkInterfaceSnapshotService>();

    public NetworkInterfaceSnapshot GetSnapshot()
    {
        try
        {
            return NetworkInterfaceSnapshot.Available(networkInterfaces.GetInterfaceNames());
        }
        catch (NetworkInformationException exception)
        {
            _logger.LogError(exception, "Failed to enumerate host network interfaces.");
            return NetworkInterfaceSnapshot.Unavailable;
        }
        catch (PlatformNotSupportedException exception)
        {
            _logger.LogError(exception, "Host network-interface enumeration is not supported on this platform.");
            return NetworkInterfaceSnapshot.Unavailable;
        }
    }
}
