using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Rules.Filtering.Tags;

internal sealed record TagRuleMatchEvidence(RuleTag Tag) : RuleMatchEvidence;
