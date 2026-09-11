using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallRuleInterfaceValidator
{
    IResponsePayload? Validate(FirewallRuleSpecification rule);
}
