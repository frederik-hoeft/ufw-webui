namespace Ufw.Client.RuleInsertion;

internal sealed record RuleInsertionNavigationResolution(OrderedRuleInsertionNavigationContext? Context, OrderedRuleInsertionContextError Error)
{
    public bool Succeeded => Context is not null;
}
