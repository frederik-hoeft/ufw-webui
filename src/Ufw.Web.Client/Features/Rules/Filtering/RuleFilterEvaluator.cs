namespace Ufw.Web.Client.Features.Rules.Filtering;

internal abstract class RuleFilterEvaluator<TFilter> : IRuleFilterEvaluator where TFilter : RuleFilter
{
    public Type FilterType => typeof(TFilter);

    public RuleMatchEvaluation Evaluate(RuleRowProjection row, RuleFilter filter, RuleFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(context);
        if (filter is not TFilter typedFilter)
        {
            throw new ArgumentException($"Expected filter type {typeof(TFilter).Name}.", nameof(filter));
        }
        return Evaluate(row, typedFilter, context);
    }

    protected abstract RuleMatchEvaluation Evaluate(RuleRowProjection row, TFilter filter, RuleFilterContext context);
}
