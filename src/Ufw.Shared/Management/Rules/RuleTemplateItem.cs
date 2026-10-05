using Ufw.Shared.Firewall;

namespace Ufw.Shared.Management.Rules;

public sealed class RuleTemplateItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public FirewallRuleSpecification Rule { get; init; } = new();

    public string? Notes { get; init; }

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];

    public RuleGroupSummary? Group { get; init; }
}
