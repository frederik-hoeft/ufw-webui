namespace Ufw.Shared.Firewall.SystemState;

/// <summary>Indicates why a structural root traversal could not be fully materialized.</summary>
[Flags]
public enum FilterTraversalStatus
{
    /// <summary>The root was completely expanded; no structural traversal limits were reached.</summary>
    None = 0,
    RuleLimitReached = 1,
    DepthLimitReached = 2,
    CycleDetected = 4,
}
