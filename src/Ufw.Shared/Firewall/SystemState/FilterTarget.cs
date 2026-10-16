namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Jump, goto, or extension target at the end of a rule.
/// <see cref="IsChainReference"/> is true only when <see cref="Name"/> is a chain in the same table.
/// </summary>
public sealed class FilterTarget
{
    public required FilterTargetKind Kind { get; init; }

    public string? Name { get; init; }

    public required bool IsChainReference { get; init; }

    /// <summary>
    /// Uninterpreted target options, such as <c>--reject-with</c> or <c>--log-prefix</c>.
    /// </summary>
    public string? Arguments { get; init; }
}
