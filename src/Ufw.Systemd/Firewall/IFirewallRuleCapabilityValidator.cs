using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Firewall;

internal interface IFirewallRuleCapabilityValidator
{
    IResponsePayload? Validate(FirewallRuleSpecification rule, FirewallConfigurationSnapshot configuration);
}
