using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

/// <summary>
/// State-dependent rule replacement intent. The target is a zero-based occurrence in the fingerprinted baseline.
/// </summary>
public sealed class ReplaceRulePayload
{
    public required string BaselineFingerprint { get; set; }

    public int TargetOccurrenceId { get; set; }

    public required string OriginalRuleId { get; set; }

    public required FirewallRuleSpecification ReplacementRule { get; set; }
}
