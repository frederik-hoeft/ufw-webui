using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Web.Services.Status;

internal sealed class StatusDaemonGateway(IUfwClient ufwClient) : IStatusDaemonGateway
{
    private const string STATUS_ROUTE = "/api/v1/status";

    public Task GetStatusAsync(CancellationToken cancellationToken = default) =>
        ufwClient.SendAsync(RequestMethod.Get, STATUS_ROUTE, cancellationToken);
}
