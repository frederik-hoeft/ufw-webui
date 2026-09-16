namespace Ufw.Client.Rules.Filtering;

internal sealed record PortRuleFilter(RuleEndpointField Endpoint, RulePortSet Ports) : RuleFilter;
