using System.Net.NetworkInformation;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Systemd.NetworkInterfaces;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/network-interfaces")]
internal sealed class NetworkInterfacesController(
    INetworkInterfaceProvider networkInterfaces,
    ILogger logger) : ControllerBase
{
    private readonly ILogger<NetworkInterfacesController> _logger = logger.Scoped<NetworkInterfacesController>();

    [Get]
    public ValueTask<IResponsePayload> GetNetworkInterfaces(CancellationToken cancellationToken)
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
