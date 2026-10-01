using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Templates;

public sealed class RuleTemplateDraft
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public FirewallRuleSpecification Rule { get; init; } = null!;

    public string? Notes { get; set; }

    public IReadOnlyList<Guid> TagIds { get; set; } = [];

    public Guid? GroupId { get; set; }
}
