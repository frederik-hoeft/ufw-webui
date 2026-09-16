namespace Ufw.Client.Rules.Filtering;

internal interface IRuleTextSearchService
{
    RuleMatchEvaluation Evaluate(RuleRowProjection row, IReadOnlyList<string> terms);
}
