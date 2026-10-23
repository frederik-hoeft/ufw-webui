namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One UFW user rule and the iptables lines <c>user.rules</c> or <c>user6.rules</c> uses to install it.
/// </summary>
public sealed class UfwUserRuleTranslation
{
    public required int Index { get; init; }

    public FirewallRuleSpecification? Specification { get; init; }

    public string? RuleId { get; init; }

    public required IReadOnlyList<TranslatedIptablesRule> IptablesRules { get; init; }
}
