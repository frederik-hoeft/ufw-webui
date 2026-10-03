using Ufw.Shared.Firewall;

namespace Ufw.Web.Data.Access.Rules.Templates;

public sealed record RuleTemplateValues(
    string Name,
    string? Description,
    FirewallRuleSpecification Rule,
    string? Notes,
    IReadOnlyList<Guid> TagIds,
    Guid? GroupId);
