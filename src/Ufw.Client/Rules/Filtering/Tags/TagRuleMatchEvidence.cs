using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Tags;

internal sealed record TagRuleMatchEvidence(string Tag) : RuleMatchEvidence;
