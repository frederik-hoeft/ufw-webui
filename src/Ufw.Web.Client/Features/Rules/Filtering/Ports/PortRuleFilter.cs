using Ufw.Web.Client.Features.Rules.Filtering.Semantics;

namespace Ufw.Web.Client.Features.Rules.Filtering.Ports;

internal sealed record PortRuleFilter(RuleEndpointField Endpoint, PortFilterOperand Ports) : RuleFilter;
