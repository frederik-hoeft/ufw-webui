namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Structural kind of one filter clause on an iptables rule.
/// Opaque clauses are preserved text, not an interpreted match.
/// </summary>
public enum FilterClauseKind
{
    SourceAddress,
    DestinationAddress,
    Protocol,
    SourcePort,
    DestinationPort,
    EitherPort,
    IngressInterface,
    EgressInterface,
    Opaque,
}
