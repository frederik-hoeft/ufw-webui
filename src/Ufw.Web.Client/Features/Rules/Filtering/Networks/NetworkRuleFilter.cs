namespace Ufw.Web.Client.Features.Rules.Filtering.Networks;

internal sealed record NetworkRuleFilter(RuleEndpointField Endpoint, RuleNetwork Network) : RuleFilter;
