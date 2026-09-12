using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderService
{
    ValueTask<IResponsePayload> ReorderAsync(ReorderRulesRequest request, CancellationToken cancellationToken);
}
