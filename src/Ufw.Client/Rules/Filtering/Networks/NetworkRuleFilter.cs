using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Networks;

internal sealed record NetworkRuleFilter(RuleEndpointField Endpoint, RuleNetwork Network) : RuleFilter;
