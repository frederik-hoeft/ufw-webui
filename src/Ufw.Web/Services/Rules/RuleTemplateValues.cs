using Ufw.Shared.Firewall;

namespace Ufw.Web.Services.Rules;

internal sealed record RuleTemplateValues(string Name, string? Description, FirewallRuleSpecification Rule, RuleMetadataValues Metadata);
