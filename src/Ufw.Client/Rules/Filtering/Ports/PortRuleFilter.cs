using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Ports;

internal sealed record PortRuleFilter(RuleEndpointField Endpoint, RulePortSet Ports) : RuleFilter;
