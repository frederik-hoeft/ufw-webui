namespace Ufw.Shared.Security.Intent;

/// <summary>
/// State-dependent rule reordering intent. Occurrence IDs are zero-based positions in the fingerprinted baseline.
/// </summary>
public sealed class ReorderRulesPayload
{
    public required string BaselineFingerprint { get; set; }

    public required int[] DesiredOrder { get; set; }
}
