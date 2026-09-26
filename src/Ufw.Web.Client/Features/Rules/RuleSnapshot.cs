using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleSnapshot(
    bool FirewallActive,
    IReadOnlyList<ListedFirewallRule> Rules,
    FirewallConfigurationSnapshot Configuration,
    IReadOnlyDictionary<string, RuleMetadata> Metadata,
    DateTimeOffset CapturedAt)
{
    public RuleSnapshot(bool firewallActive, IReadOnlyList<ListedFirewallRule> rules, FirewallConfigurationSnapshot configuration)
        : this(firewallActive, rules, configuration, new Dictionary<string, RuleMetadata>(StringComparer.Ordinal), default)
    {
    }

    public RuleSnapshot(bool firewallActive, IReadOnlyList<ListedFirewallRule> rules, FirewallConfigurationSnapshot configuration, IReadOnlyDictionary<string, RuleMetadata> metadata)
        : this(firewallActive, rules, configuration, metadata, default)
    {
    }

    public static RuleSnapshot FromResponse(RuleInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Firewall);

        Dictionary<string, RuleMetadata> metadata = new(StringComparer.Ordinal);
        foreach (RuleMetadataItem item in response.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.RuleId) || !metadata.TryAdd(item.RuleId, FromMetadataItem(item)))
            {
                throw new ApiProtocolException("The enriched rule response contains invalid or duplicate metadata identities.");
            }
        }

        return FromFirewallResponse(response.Firewall, metadata, response.CapturedAt);
    }

    public RuleSnapshot ApplyMetadataMutation(string ruleId, RuleMetadataMutationResponse response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(response);
        if (!Rules.Any(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Rule metadata cannot be applied to a rule outside the current snapshot.");
        }

        Dictionary<string, RuleMetadata> metadata = new(Metadata, StringComparer.Ordinal);
        if (response.Metadata is null)
        {
            metadata.Remove(ruleId);
        }
        else
        {
            if (!string.Equals(response.Metadata.RuleId, ruleId, StringComparison.Ordinal))
            {
                throw new ApiProtocolException("The metadata mutation response refers to a different rule identity.");
            }
            metadata[ruleId] = FromMetadataItem(response.Metadata);
        }

        return this with { Metadata = metadata };
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

    private static RuleMetadata FromMetadataItem(RuleMetadataItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Tags is null)
        {
            throw new ApiProtocolException("The enriched rule response contains metadata without a tag list.");
        }

        List<RuleTag> tags = new(item.Tags.Count);
        foreach (RuleTagItem tag in item.Tags)
        {
            if (tag is null
                || tag.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(tag.Name)
                || !RuleTagColor.TryNormalize(tag.Color, out string? color))
            {
                throw new ApiProtocolException("The enriched rule response contains invalid metadata.");
            }
            tags.Add(new RuleTag(tag.Id, tag.Name.Trim(), color));
        }

        RuleGroupMembership? group = FromGroupSummary(item.Group);
        if (item.Id == Guid.Empty || tags.Select(static tag => tag.Id).Distinct().Count() != tags.Count)
        {
            throw new ApiProtocolException("The enriched rule response contains invalid metadata.");
        }

        return new RuleMetadata(item.Id, string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(), tags, group);
    }

    private static RuleGroupMembership? FromGroupSummary(RuleGroupSummary? group)
    {
        if (group is null)
        {
            return null;
        }
        if (group.Id == Guid.Empty || string.IsNullOrWhiteSpace(group.Name))
        {
            throw new ApiProtocolException("The enriched rule response contains invalid group metadata.");
        }

        return new RuleGroupMembership(group.Id, group.Name.Trim(), string.IsNullOrWhiteSpace(group.Comment) ? null : group.Comment.Trim());
    }

    public static RuleSnapshot FromFirewallResponse(RuleListResponse response, IReadOnlyDictionary<string, RuleMetadata>? metadata = null, DateTimeOffset capturedAt = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        Dictionary<string, RuleMetadata> liveMetadata = new(StringComparer.Ordinal);
        if (metadata is not null)
        {
            HashSet<string> liveRuleIds = [.. response.Rules
                .Select(static rule => rule.RuleId)
                .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
                .Cast<string>()];
            foreach ((string ruleId, RuleMetadata value) in metadata)
            {
                if (liveRuleIds.Contains(ruleId))
                {
                    liveMetadata.Add(ruleId, value);
                }
            }
        }

        return new(response.Active, response.Rules.ToArray(), response.Configuration, liveMetadata, capturedAt);
    }
}
