using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Api.Controllers;

internal sealed partial class RulesController(IFirewallRuleQueryService firewallRules, IFirewallMutationService firewallMutations) : ControllerBase
{
    public partial ValueTask<IResponsePayload> GetRulesAsync(CancellationToken cancellationToken) =>
        firewallRules.ListAsync(cancellationToken);

    public partial ValueTask<IResponsePayload> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken) =>
        firewallMutations.AddAsync(request, cancellationToken);

    public partial ValueTask<IResponsePayload> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken) =>
        firewallMutations.DeleteAsync(request, cancellationToken);
}
