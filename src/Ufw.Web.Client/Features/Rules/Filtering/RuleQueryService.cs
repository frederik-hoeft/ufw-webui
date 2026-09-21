using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed class RuleQueryService : IRuleQueryService
{
    private readonly IReadOnlyDictionary<Type, IRuleFilterEvaluator> _evaluators;

    public RuleQueryService(IEnumerable<IRuleFilterEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);
        _evaluators = evaluators.ToDictionary(static evaluator => evaluator.FilterType);
    }

    public RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query, IReadOnlyList<KnownHostInventoryItem>? knownHosts = null)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(query);

        RuleFilterContext context = new(family.AddressFamily, knownHosts ?? []);
        IReadOnlyList<ConfiguredEvaluator> configuredEvaluators = ResolveEvaluators(query.Filters);
        List<RuleQueryRow> visibleRows = [];
        foreach (RuleRowProjection row in family.Rows)
        {
            List<RuleMatchEvidence> evidence = [];
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
