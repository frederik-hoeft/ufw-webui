using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules;

/// <summary>
/// Maps REST/IPC read and mutation responses into transport-independent snapshots.
/// </summary>
internal static class RuleSnapshotFactory
{
    public static RuleSnapshot FromInventoryResponse(RuleInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Firewall);

        Dictionary<string, RuleMetadata> metadata = new(StringComparer.Ordinal);
        foreach (RuleMetadataItem item in response.Metadata)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.RuleId)
                || !metadata.TryAdd(item.RuleId, RuleMetadataProtocolMapper.MapMetadata(item, "The enriched rule response contains invalid metadata.")))
            {
                throw new ApiProtocolException("The enriched rule response contains invalid or duplicate metadata identities.");
            }
        }

        return FromFirewallResponse(response.Firewall, metadata, response.CapturedAt);
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

        return new RuleSnapshot(response.Active, response.Rules.ToArray(), response.Configuration, liveMetadata, capturedAt)
        {
            Assessment = response.Assessment,
        };
    }

    public static RuleListResponse ToFirewallResponse(RuleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RuleListResponse(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration) { Assessment = snapshot.Assessment };
    }

    public static RuleSnapshot ApplyMetadataMutation(RuleSnapshot snapshot, string ruleId, RuleMetadataMutationResponse response)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(response);
        if (!snapshot.Rules.Any(rule => string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Rule metadata cannot be applied to a rule outside the current snapshot.");
        }

        Dictionary<string, RuleMetadata> metadata = new(snapshot.Metadata, StringComparer.Ordinal);
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
            metadata[ruleId] = RuleMetadataProtocolMapper.MapMetadata(response.Metadata, "The metadata mutation response contains invalid metadata.");
        }

        return snapshot with { Metadata = metadata };
    }
}
