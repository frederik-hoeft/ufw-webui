using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Groups;

internal sealed record GroupRuleMatchEvidence(string Group) : RuleMatchEvidence;
