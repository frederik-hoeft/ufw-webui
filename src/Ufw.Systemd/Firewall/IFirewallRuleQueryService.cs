using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallRuleQueryService
{
    ValueTask<IResponsePayload> ListAsync(CancellationToken cancellationToken);
}
