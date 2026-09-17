namespace Ufw.Client.Rules.Filtering;

internal interface IRuleFilterEvaluator
{
    Type FilterType { get; }

    RuleMatchEvaluation Evaluate(RuleRowProjection row, RuleFilter filter, RuleFilterContext context);
}
