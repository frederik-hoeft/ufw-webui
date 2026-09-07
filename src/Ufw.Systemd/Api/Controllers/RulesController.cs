using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Controllers.Routing;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Api.Controllers;

[Route("api/v1/rules")]
internal sealed class RulesController(IFirewallMutationService firewall) : ControllerBase
{
    [Get]
    public ValueTask<IResponsePayload> GetRulesAsync(CancellationToken cancellationToken) =>
        firewall.ListAsync(cancellationToken);

    [Post]
    public ValueTask<IResponsePayload> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken) =>
        firewall.AddAsync(request, cancellationToken);

    [Delete]
    public ValueTask<IResponsePayload> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken) =>
        firewall.DeleteAsync(request, cancellationToken);
}
