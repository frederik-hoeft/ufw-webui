namespace Ufw.Client.Rules.Filtering;

internal sealed class RuleQueryService : IRuleQueryService
{
    private readonly IReadOnlyDictionary<Type, IRuleFilterEvaluator> _evaluators;
    private readonly IRuleTextSearchService _textSearch;

    public RuleQueryService(IEnumerable<IRuleFilterEvaluator> evaluators, IRuleTextSearchService textSearch)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        _textSearch = textSearch ?? throw new ArgumentNullException(nameof(textSearch));
        _evaluators = evaluators.ToDictionary(static evaluator => evaluator.FilterType);
    }

    public RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(query);

        RuleFilterContext context = new(family.AddressFamily);
        IReadOnlyList<ConfiguredEvaluator> configuredEvaluators = ResolveEvaluators(query.Filters);
        List<RuleQueryRow> visibleRows = [];
        foreach (RuleRowProjection row in family.Rows)
        {
            RuleMatchEvaluation textResult = _textSearch.Evaluate(row, query.TextTerms);
            if (!textResult.Matches)
            {
                continue;
            }

            List<RuleMatchEvidence> evidence = [.. textResult.Evidence];
            bool matches = true;
            foreach (ConfiguredEvaluator configured in configuredEvaluators)
            {
                RuleMatchEvaluation result = configured.Evaluator.Evaluate(row, configured.Filter, context);
                if (!result.Matches)
                {
                    matches = false;
                    break;
                }
                evidence.AddRange(result.Evidence);
            }

            if (matches)
            {
                visibleRows.Add(new RuleQueryRow(row, evidence));
            }
        }

        return new RuleFamilyQueryResult(family.AddressFamily, visibleRows, family.Rows.Count);
    }

    private IReadOnlyList<ConfiguredEvaluator> ResolveEvaluators(IReadOnlyList<RuleFilter> filters)
    {
        List<ConfiguredEvaluator> configured = new(filters.Count);
        foreach (RuleFilter filter in filters)
        {
            if (!_evaluators.TryGetValue(filter.GetType(), out IRuleFilterEvaluator? evaluator))
            {
                throw new InvalidOperationException($"No rule filter evaluator is registered for {filter.GetType().Name}.");
            }
            configured.Add(new ConfiguredEvaluator(filter, evaluator));
        }
        return configured;
    }

    private sealed record ConfiguredEvaluator(RuleFilter Filter, IRuleFilterEvaluator Evaluator);
}
