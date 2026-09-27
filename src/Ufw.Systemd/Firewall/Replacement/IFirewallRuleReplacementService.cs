using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;

namespace Ufw.Systemd.Firewall.Replacement;

internal interface IFirewallRuleReplacementService
{
    ValueTask<IResponsePayload> ReplaceAsync(ReplaceRuleRequest request, CancellationToken cancellationToken);
}
