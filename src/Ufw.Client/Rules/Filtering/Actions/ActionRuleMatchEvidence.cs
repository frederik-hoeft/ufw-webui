using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Actions;

internal sealed record ActionRuleMatchEvidence(string Value) : RuleMatchEvidence;
