using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallMutationService
{
    ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken);

    ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken);
}
