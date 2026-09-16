using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Directions;

internal sealed record DirectionRuleMatchEvidence(string Value) : RuleMatchEvidence;
