using Ufw.Web.Client.Features.Rules.Filtering.Semantics;

namespace Ufw.Web.Client.Features.Rules.Filtering.Networks;

internal sealed record NetworkRuleFilter(RuleEndpointField Endpoint, NetworkFilterOperand Network) : RuleFilter;
