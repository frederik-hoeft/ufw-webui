namespace Ufw.Shared.Domain;

/// <summary>
/// One quadrant of a policy diff. Every queried packet belongs to exactly one kind.
/// </summary>
/// <remarks>
/// Decision changes and responsibility shifts are independent. <see cref="DecisionOnly"/> and <see cref="Both"/>
/// together are the observable policy difference. <see cref="ResponsibilityOnly"/> and <see cref="Both"/> together
/// are the semantic provenance difference. Those two sets can overlap on <see cref="Both"/>.
/// </remarks>
public enum PolicyDiffKind
{
    /// <summary>The effective decision and the semantic responsibility are both unchanged.</summary>
    Unchanged = 0,

    /// <summary>The effective decision differs and the semantic responsibility does not.</summary>
    DecisionOnly = 1,

    /// <summary>The semantic responsibility differs and the effective decision does not.</summary>
    ResponsibilityOnly = 2,

    /// <summary>The effective decision and the semantic responsibility both differ.</summary>
    Both = 3,
}
