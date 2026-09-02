using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Requests.Domain;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallMutationService
{
    ValueTask<IResponsePayload> ListAsync(CancellationToken cancellationToken);

    ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken);

    ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken);
}
