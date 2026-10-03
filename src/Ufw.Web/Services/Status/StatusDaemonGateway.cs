using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Status;

internal sealed class StatusDaemonGateway(IUfwClient ufwClient) : IStatusDaemonGateway
{
    private const string STATUS_ROUTE = "/api/v1/status";

    public Task<DaemonResult> GetStatusAsync(CancellationToken cancellationToken = default) =>
        DaemonResult.CaptureAsync(() => ufwClient.SendAsync(RequestMethod.Get, STATUS_ROUTE, cancellationToken));
}
