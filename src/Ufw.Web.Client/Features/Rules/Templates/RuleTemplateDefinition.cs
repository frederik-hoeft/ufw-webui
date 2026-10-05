using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Templates;

public sealed record RuleTemplateDefinition(
    string Name,
    string? Description,
    FirewallRuleSpecification Rule,
    string? Notes,
    IReadOnlyList<Guid> TagIds,
    Guid? GroupId);
