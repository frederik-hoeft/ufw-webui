namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One iptables line written for a UFW tuple. Chain references are not resolved here;
/// the live table is the authority for which names are chains.
/// </summary>
public sealed class TranslatedIptablesRule
{
    public required string ChainName { get; init; }

    public required IReadOnlyList<FilterClause> Clauses { get; init; }

    public required FilterTarget Target { get; init; }
}
