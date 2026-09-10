using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class RulesController(IFirewallMutationService firewall) : ControllerBase
{
    public partial ValueTask<IResponsePayload> GetRulesAsync(CancellationToken cancellationToken) =>
        firewall.ListAsync(cancellationToken);

    public partial ValueTask<IResponsePayload> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken) =>
        firewall.AddAsync(request, cancellationToken);

    public partial ValueTask<IResponsePayload> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken) =>
        firewall.DeleteAsync(request, cancellationToken);
}
