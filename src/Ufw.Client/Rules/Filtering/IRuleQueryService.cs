namespace Ufw.Client.Rules.Filtering;

internal interface IRuleQueryService
{
    RuleFamilyQueryResult Evaluate(RuleFamilyProjection family, RuleQuery query);
}
