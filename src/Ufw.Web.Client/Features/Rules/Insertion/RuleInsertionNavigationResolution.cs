namespace Ufw.Web.Client.Features.Rules.Insertion;

internal sealed record RuleInsertionNavigationResolution(OrderedRuleInsertionNavigationContext? Context, OrderedRuleInsertionContextError Error)
{
    public bool Succeeded => Context is not null;
}
