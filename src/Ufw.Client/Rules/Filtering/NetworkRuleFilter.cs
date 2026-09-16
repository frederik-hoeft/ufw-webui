namespace Ufw.Client.Rules.Filtering;

internal sealed record NetworkRuleFilter(RuleEndpointField Endpoint, RuleNetwork Network) : RuleFilter;
