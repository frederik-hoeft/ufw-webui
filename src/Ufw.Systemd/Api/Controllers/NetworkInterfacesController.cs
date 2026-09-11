using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.NetworkInterfaces;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class NetworkInterfacesController(INetworkInterfaceSnapshotService networkInterfaces) : ControllerBase
{
    public partial ValueTask<IResponsePayload> GetNetworkInterfacesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NetworkInterfaceSnapshot snapshot = networkInterfaces.GetSnapshot();
        IResponsePayload response = snapshot.IsAvailable
            ? new NetworkInterfaceListResponse(snapshot.Interfaces)
            : new InternalServerErrorResponse("Failed to enumerate host network interfaces.");
        return ValueTask.FromResult(response);
    }
}
