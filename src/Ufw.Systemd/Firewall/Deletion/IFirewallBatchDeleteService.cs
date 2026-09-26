using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Systemd.Firewall.Deletion;

internal interface IFirewallBatchDeleteService
{
    ValueTask<IResponsePayload> DeleteAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken);
}
