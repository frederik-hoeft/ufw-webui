using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.NetworkInterfaces;

/// <summary>
/// Provides validated network-interface state from the daemon while keeping IPC route details private to the gateway.
/// </summary>
internal interface INetworkInterfaceDaemonGateway
{
    Task<DaemonResult<IReadOnlyList<string>>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default);
}
