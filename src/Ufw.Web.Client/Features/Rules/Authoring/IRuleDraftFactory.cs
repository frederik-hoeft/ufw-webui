using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal interface IRuleDraftFactory
{
    FirewallRuleSpecification Create();

    FirewallRuleSpecification CreateFromExisting(FirewallRuleSpecification rule);
}
