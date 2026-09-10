using System.Net.NetworkInformation;
using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.NetworkInterfaces;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class NetworkInterfacesController(INetworkInterfaceProvider networkInterfaces, ILogger logger) : ControllerBase
{
    private readonly ILogger<NetworkInterfacesController> _logger = logger.Scoped<NetworkInterfacesController>();

    public partial ValueTask<IResponsePayload> GetNetworkInterfacesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult<IResponsePayload>(new NetworkInterfaceListResponse(networkInterfaces.GetInterfaceNames()));
        }
        catch (NetworkInformationException exception)
        {
            _logger.LogError(exception, "Failed to enumerate host network interfaces.");
            return ValueTask.FromResult<IResponsePayload>(new InternalServerErrorResponse("Failed to enumerate host network interfaces."));
        }
        catch (PlatformNotSupportedException exception)
        {
            _logger.LogError(exception, "Host network-interface enumeration is not supported on this platform.");
            return ValueTask.FromResult<IResponsePayload>(new InternalServerErrorResponse("Host network-interface enumeration is not supported on this platform."));
        }
    }
}
