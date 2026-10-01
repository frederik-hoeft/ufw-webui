using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal interface IRuleTemplateNameGenerator
{
    string Generate(FirewallRuleSpecification rule);
}
