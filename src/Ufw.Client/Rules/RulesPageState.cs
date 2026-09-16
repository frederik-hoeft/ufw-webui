using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Client.Rules.Metadata;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Rules;

internal enum RulesPageStatus
{
    NotLoaded,
    Loading,
    Current,
    Refreshing,
    Stale,
    Failed,
}

internal enum RuleRefreshReason
{
    Manual,
    AfterMutation,
}

internal enum RuleSnapshotStaleReason
{
    RefreshFailed,
    MutationCommitted,
    MutationOutcomeUnknown,
    MutationRejectedRequiresRefresh,
}

internal sealed record RuleSnapshot(
    bool FirewallActive,
    IReadOnlyList<ListedFirewallRule> Rules,
    FirewallConfigurationSnapshot Configuration,
    IReadOnlyDictionary<string, RuleMetadata> Metadata)
{
    public RuleSnapshot(
        bool firewallActive,
        IReadOnlyList<ListedFirewallRule> rules,
        FirewallConfigurationSnapshot configuration)
        : this(firewallActive, rules, configuration, new Dictionary<string, RuleMetadata>(StringComparer.Ordinal))
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

        return FromFirewallResponse(response.Firewall, metadata);
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

        if (item.Id == Guid.Empty || tags.Select(static tag => tag.Id).Distinct().Count() != tags.Count)
        {
            throw new ApiProtocolException("The enriched rule response contains invalid metadata.");
        }

        return new RuleMetadata(item.Id, string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim(), tags);
    }

    public static RuleSnapshot FromFirewallResponse(
        RuleListResponse response,
        IReadOnlyDictionary<string, RuleMetadata>? metadata = null)
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

        return new(response.Active, response.Rules.ToArray(), response.Configuration, liveMetadata);
    }
}

internal sealed record RulesPageState
{
    private RulesPageState(RulesPageStatus status, RuleSnapshot? snapshot, ClientError? error, RuleRefreshReason? refreshReason, RuleSnapshotStaleReason? staleReason)
    {
        Status = status;
        Snapshot = snapshot;
        Error = error;
        RefreshReason = refreshReason;
        StaleReason = staleReason;
    }

    public RulesPageStatus Status { get; }

    public RuleSnapshot? Snapshot { get; }

    public ClientError? Error { get; }

    public RuleRefreshReason? RefreshReason { get; }

    public RuleSnapshotStaleReason? StaleReason { get; }

    public bool IsLoading => Status is RulesPageStatus.Loading or RulesPageStatus.Refreshing;

    public bool IsCurrent => Status == RulesPageStatus.Current;

    public bool IsStale => Status == RulesPageStatus.Stale;

    public static RulesPageState Initial { get; } = new(RulesPageStatus.NotLoaded, snapshot: null, error: null, refreshReason: null, staleReason: null);

    public RulesPageState BeginRefresh(RuleRefreshReason reason) =>
        new(Snapshot is null ? RulesPageStatus.Loading : RulesPageStatus.Refreshing, Snapshot, error: null, refreshReason: reason, staleReason: StaleReason);

    public static RulesPageState CompleteRefresh(RuleInventoryResponse response) =>
        new(RulesPageStatus.Current, RuleSnapshot.FromResponse(response), error: null, refreshReason: null, staleReason: null);

    public RulesPageState FailRefresh(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (Snapshot is null)
        {
            return new(RulesPageStatus.Failed, snapshot: null, error, refreshReason: null, staleReason: null);
        }

        RuleSnapshotStaleReason staleReason = RefreshReason == RuleRefreshReason.AfterMutation
            ? RuleSnapshotStaleReason.MutationCommitted
            : StaleReason ?? RuleSnapshotStaleReason.RefreshFailed;
        return new(RulesPageStatus.Stale, Snapshot, error, refreshReason: null, staleReason);
    }

    public RulesPageState AfterMetadataMutation(string ruleId, RuleMetadataMutationResponse response)
    {
        if (Snapshot is null)
        {
            throw new InvalidOperationException("Rule metadata cannot be updated against an unloaded rule snapshot.");
        }

        return new RulesPageState(Status, Snapshot.ApplyMetadataMutation(ruleId, response), Error, RefreshReason, StaleReason);
    }

    public RulesPageState ReconcileTagCatalog(IReadOnlyList<RuleTag> tags)
    {
        if (Snapshot is null)
        {
            return this;
        }
        return new RulesPageState(Status, Snapshot.ReconcileTagCatalog(tags), Error, RefreshReason, StaleReason);
    }

    public RulesPageState AfterInsertion(RuleInsertionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.FinalSnapshot is not null)
        {
            RuleSnapshot snapshot = RuleSnapshot.FromFirewallResponse(response.FinalSnapshot, Snapshot?.Metadata);
            return new RulesPageState(RulesPageStatus.Current, snapshot, error: null, refreshReason: null, staleReason: null);
        }

        if (Snapshot is null)
        {
            throw new InvalidOperationException("An insertion response cannot replace an unloaded rule snapshot.");
        }

        return new RulesPageState(
            RulesPageStatus.Stale,
            Snapshot,
            error: null,
            refreshReason: null,
            staleReason: RuleSnapshotStaleReason.MutationOutcomeUnknown);
    }

    public RulesPageState AfterReorder(RuleReorderResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.FinalSnapshot is not null)
        {
            RuleSnapshot snapshot = RuleSnapshot.FromFirewallResponse(response.FinalSnapshot, Snapshot?.Metadata);
            return new RulesPageState(RulesPageStatus.Current, snapshot, error: null, refreshReason: null, staleReason: null);
        }

        if (Snapshot is null)
        {
            throw new InvalidOperationException("A reorder response cannot replace an unloaded rule snapshot.");
        }

        return new RulesPageState(
            RulesPageStatus.Stale,
            Snapshot,
            error: null,
            refreshReason: null,
            staleReason: RuleSnapshotStaleReason.MutationOutcomeUnknown);
    }

    public RulesPageState AfterMutationFailure(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (Snapshot is null)
        {
            throw new InvalidOperationException("A mutation cannot fail against an unloaded rule snapshot.");
        }

        RuleSnapshotStaleReason? staleReason = error.Kind switch
        {
            ClientErrorKind.Unavailable or ClientErrorKind.Protocol or ClientErrorKind.Canceled
                => RuleSnapshotStaleReason.MutationOutcomeUnknown,
            ClientErrorKind.Conflict => RuleSnapshotStaleReason.MutationRejectedRequiresRefresh,
            ClientErrorKind.RequestRejected when error.Retryable
                => RuleSnapshotStaleReason.MutationRejectedRequiresRefresh,
            _ => null,
        };

        return staleReason is null
            ? this
            : new RulesPageState(RulesPageStatus.Stale, Snapshot, error, refreshReason: null, staleReason: staleReason.Value);
    }
}
