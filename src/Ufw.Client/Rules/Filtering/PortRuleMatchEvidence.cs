namespace Ufw.Client.Rules.Filtering;

internal sealed record PortRuleMatchEvidence(RuleEndpointField Endpoint, string RulePorts, string QueryPorts) : RuleMatchEvidence;
