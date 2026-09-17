using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Protocols;

internal sealed record ProtocolRuleMatchEvidence(string Value) : RuleMatchEvidence;
