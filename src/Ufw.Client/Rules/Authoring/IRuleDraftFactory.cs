using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Authoring;

internal interface IRuleDraftFactory
{
    FirewallRuleSpecification Create();
}
