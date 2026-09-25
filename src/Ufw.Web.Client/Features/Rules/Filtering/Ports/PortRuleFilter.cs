namespace Ufw.Web.Client.Features.Rules.Filtering.Ports;

internal sealed record PortRuleFilter(RuleEndpointField Endpoint, RulePortSet Ports) : RuleFilter;
