namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleDropTargetProjection
{
    public RuleDropTargetProjection(RuleRowProjection source, RuleRowProjection target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.AddressFamily != target.AddressFamily)
        {
            throw new ArgumentException("The source and target rule must belong to the same address family.", nameof(target));
        }
        if (source.FamilyPosition == target.FamilyPosition)
        {
            throw new ArgumentException("The source and target rule must have different family positions.", nameof(target));
        }

        Row = target;
        IndicatorEdge = source.FamilyPosition < target.FamilyPosition
            ? RuleDropIndicatorEdge.After
            : RuleDropIndicatorEdge.Before;
    }

    public RuleRowProjection Row { get; }

    public RuleDropIndicatorEdge IndicatorEdge { get; }
}

public enum RuleDropIndicatorEdge
{
    Before,
    After,
}
