using Ufw.Shared.Firewall;

namespace Ufw.Web.Model.V1.RuleTemplates;

public abstract class RuleTemplateRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public FirewallRuleSpecification Rule { get; init; } = new();

    public string? Notes { get; init; }

    public IReadOnlyList<Guid> TagIds { get; init; } = [];

    public Guid? GroupId { get; init; }
}
