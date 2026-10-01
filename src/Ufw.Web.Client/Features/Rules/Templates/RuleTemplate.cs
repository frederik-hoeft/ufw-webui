using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Templates;

public sealed record RuleTemplate(
    Guid Id,
    string Name,
    string? Description,
    FirewallRuleSpecification Rule,
    string? Notes,
    IReadOnlyList<RuleTag> Tags,
    RuleGroupMembership? Group)
{
    public IReadOnlyList<Guid> TagIds => Tags.Select(static tag => tag.Id).ToArray();

    public Guid? GroupId => Group?.Id;
}
