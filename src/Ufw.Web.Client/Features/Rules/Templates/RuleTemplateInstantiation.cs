using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed record RuleTemplateInstantiation(
    FirewallRuleSpecification Rule,
    string? Notes,
    IReadOnlyList<Guid> TagIds,
    Guid? GroupId);
