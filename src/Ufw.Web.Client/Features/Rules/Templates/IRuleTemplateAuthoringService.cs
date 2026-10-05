using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal interface IRuleTemplateAuthoringService
{
    RuleTemplateDefinition CreateDefinition(string name, string? description, FirewallRuleSpecification rule, RuleMetadata? metadata);

    RuleTemplateInstantiationResult Initialize(RuleTemplate ruleTemplate, FirewallAddressFamily? requiredFamily = null);
}
