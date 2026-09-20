namespace Ufw.Client.Rules.Filtering.Ports;

internal sealed record PortRuleMatchEvidence(RuleEndpointField Endpoint, string RulePorts, string QueryPorts) : RuleMatchEvidence;
