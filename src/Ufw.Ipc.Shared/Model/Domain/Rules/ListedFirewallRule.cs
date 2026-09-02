namespace Ufw.Ipc.Shared.Model.Domain.Rules;

/// <summary>
/// A rule as observed from UFW. <see cref="DisplayNumber"/> is the current
/// <c>ufw status numbered</c> index and is not a stable delete identifier.
/// </summary>
public sealed class ListedFirewallRule
{
    public string? RuleId { get; set; }

    public int? DisplayNumber { get; set; }

    public bool Parsed { get; set; }

    public string RawLine { get; set; } = string.Empty;

    public FirewallRuleSpecification? Rule { get; set; }
}
