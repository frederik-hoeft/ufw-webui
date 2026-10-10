using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleSnapshot(
    bool FirewallActive,
    IReadOnlyList<ListedFirewallRule> Rules,
    FirewallConfigurationSnapshot Configuration,
    IReadOnlyDictionary<string, RuleMetadata> Metadata,
    DateTimeOffset CapturedAt)
{
    public FirewallStateAssessment Assessment { get; init; } = FirewallStateAssessment.Clean;

    public RuleSnapshot(bool firewallActive, IReadOnlyList<ListedFirewallRule> rules, FirewallConfigurationSnapshot configuration)
        : this(firewallActive, rules, configuration, new Dictionary<string, RuleMetadata>(StringComparer.Ordinal), default)
    {
    }

    public RuleSnapshot(bool firewallActive, IReadOnlyList<ListedFirewallRule> rules, FirewallConfigurationSnapshot configuration, IReadOnlyDictionary<string, RuleMetadata> metadata)
        : this(firewallActive, rules, configuration, metadata, default)
    {
    }

    public RuleSnapshot ReconcileTagCatalog(IReadOnlyList<RuleTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        Dictionary<Guid, RuleTag> byId = tags.ToDictionary(static tag => tag.Id);
        Dictionary<string, RuleMetadata> metadata = new(Metadata.Count, StringComparer.Ordinal);
        foreach ((string ruleId, RuleMetadata value) in Metadata)
        {
            RuleTag[] reconciledTags = [.. value.Tags.Select(tag => byId.GetValueOrDefault(tag.Id) ?? tag)];
            metadata.Add(ruleId, value with { Tags = reconciledTags });
        }
        return this with { Metadata = metadata };
    }

    public RuleSnapshot ReconcileGroupCatalog(IReadOnlyList<RuleGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        Dictionary<Guid, RuleGroup> byId = groups.ToDictionary(static group => group.Id);
        Dictionary<string, RuleMetadata> metadata = new(Metadata.Count, StringComparer.Ordinal);
        foreach ((string ruleId, RuleMetadata value) in Metadata)
        {
            RuleGroupMembership? group = value.Group;
            if (group is not null && byId.TryGetValue(group.Id, out RuleGroup? currentGroup))
            {
                group = new RuleGroupMembership(currentGroup.Id, currentGroup.Name, currentGroup.Comment);
            }
            metadata.Add(ruleId, value with { Group = group });
        }
        return this with { Metadata = metadata };
    }
}
