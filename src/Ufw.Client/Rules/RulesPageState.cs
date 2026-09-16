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
            RuleTag[] tags = [.. item.Tags.Select(static tag => new RuleTag(tag.Id, tag.Name, tag.Color))];
            if (item.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(item.RuleId)
                || tags.Any(static tag => tag.Id == Guid.Empty || string.IsNullOrWhiteSpace(tag.Name) || string.IsNullOrWhiteSpace(tag.Color))
                || !metadata.TryAdd(item.RuleId, new RuleMetadata(item.Id, item.Notes, tags)))
            {
                throw new InvalidDataException("The enriched rule response contains invalid or duplicate metadata identities.");
            }
        }

        return FromFirewallResponse(response.Firewall, metadata);
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
