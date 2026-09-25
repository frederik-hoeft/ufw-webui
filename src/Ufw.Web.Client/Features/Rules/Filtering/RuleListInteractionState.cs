namespace Ufw.Web.Client.Features.Rules.Filtering;

internal sealed record RuleListInteractionState
{
    private RuleListInteractionState(RuleListInteractionMode mode)
    {
        Mode = mode;
    }

    public RuleListInteractionMode Mode { get; }

    public bool CanChangeQuery => Mode != RuleListInteractionMode.OrderingPreview;

    public bool CanOrder => Mode != RuleListInteractionMode.Filtered;

    public static RuleListInteractionState Resolve(bool queryActive, bool orderingPreviewActive)
    {
        if (queryActive && orderingPreviewActive)
        {
            throw new InvalidOperationException("Rule filtering and ordering preview cannot be active at the same time.");
        }
        if (orderingPreviewActive)
        {
            return new RuleListInteractionState(RuleListInteractionMode.OrderingPreview);
        }
        return new RuleListInteractionState(queryActive ? RuleListInteractionMode.Filtered : RuleListInteractionMode.Normal);
    }
}
