namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// How a rule names its target. This is chain structure, not a packet verdict.
/// </summary>
public enum FilterTargetKind
{
    None,
    Jump,
    Goto,
}
