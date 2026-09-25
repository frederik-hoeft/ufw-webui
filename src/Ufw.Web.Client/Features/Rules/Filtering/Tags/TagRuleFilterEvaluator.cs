using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Tags;

internal sealed class TagRuleFilterEvaluator : RuleFilterEvaluator<TagRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, TagRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        RuleTag? tag = row.Metadata?.Tags.FirstOrDefault(candidate => candidate.Id == filter.Tag.Id);
        return tag is null
            ? RuleMatchEvaluation.NoMatch
            : RuleMatchEvaluation.Match(new TagRuleMatchEvidence(tag));
    }
}
