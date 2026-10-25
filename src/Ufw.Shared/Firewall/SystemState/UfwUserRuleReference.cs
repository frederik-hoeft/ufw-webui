namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Enrichment linking one live iptables rule back to the UFW user rule that translated into it.
/// <see cref="Specification"/> and <see cref="RuleId"/> are set only when that tuple fits the mutable rule model.
/// </summary>
public sealed class UfwUserRuleReference
{
    /// <summary>Zero-based index of the tuple in the user-rules file.</summary>
    public required int UserRuleIndex { get; init; }

    /// <summary>Zero-based index of this iptables line among the tuple's translations.</summary>
    public required int TranslationIndex { get; init; }

    /// <summary>How many iptables lines the tuple translated into.</summary>
    public required int TranslationCount { get; init; }

    public string? RuleId { get; init; }

    public FirewallRuleSpecification? Specification { get; init; }
}
