namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed record RuleReplacementNavigationResolution(RuleReplacementNavigationContext? Context, RuleReplacementContextError Error)
{
    public bool Succeeded => Context is not null;
}
