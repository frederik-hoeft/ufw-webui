using Ufw.Client.Rules.Filtering;

namespace Ufw.Client.Rules.Filtering.Tags;

internal sealed class TagRuleFilterEvaluator : RuleFilterEvaluator<TagRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, TagRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        string? tag = row.Metadata?.Tags.FirstOrDefault(candidate =>
            string.Equals(candidate, filter.Tag, StringComparison.OrdinalIgnoreCase));
        return tag is null
            ? RuleMatchEvaluation.NoMatch
            : RuleMatchEvaluation.Match(new TagRuleMatchEvidence(tag));
    }
}
