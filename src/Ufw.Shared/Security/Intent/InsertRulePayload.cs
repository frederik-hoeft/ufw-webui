using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

/// <summary>
/// State-dependent ordered rule creation intent. The anchor is a zero-based occurrence in the fingerprinted baseline.
/// </summary>
public sealed class InsertRulePayload
{
    public required string BaselineFingerprint { get; set; }

    public int AnchorOccurrenceId { get; set; }

    public RuleInsertionPlacement Placement { get; set; }

    public required FirewallRuleSpecification Rule { get; set; }
}
