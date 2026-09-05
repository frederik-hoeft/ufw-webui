using Ufw.Client.Errors;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Responses.Domain;

namespace Ufw.Client.Components.Rules;

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

internal sealed record RuleSnapshot(bool FirewallActive, IReadOnlyList<ListedFirewallRule> Rules)
{
    public static RuleSnapshot FromResponse(RuleListResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new(response.Active, response.Rules.ToArray());
    }
}

internal sealed record RulesPageState
{
    private RulesPageState(
        RulesPageStatus status,
        RuleSnapshot? snapshot,
        ClientError? error,
        RuleRefreshReason? refreshReason,
        RuleSnapshotStaleReason? staleReason)
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

    public static RulesPageState Initial { get; } = new(
        RulesPageStatus.NotLoaded,
        snapshot: null,
        error: null,
        refreshReason: null,
        staleReason: null);

    public RulesPageState BeginRefresh(RuleRefreshReason reason)
    {
        return new(
            Snapshot is null ? RulesPageStatus.Loading : RulesPageStatus.Refreshing,
            Snapshot,
            error: null,
            refreshReason: reason,
            staleReason: StaleReason);
    }

    public static RulesPageState CompleteRefresh(RuleListResponse response)
    {
        return new(
            RulesPageStatus.Current,
            RuleSnapshot.FromResponse(response),
            error: null,
            refreshReason: null,
            staleReason: null);
    }

    public RulesPageState FailRefresh(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (Snapshot is null)
        {
            return new(
                RulesPageStatus.Failed,
                snapshot: null,
                error,
                refreshReason: null,
                staleReason: null);
        }

        RuleSnapshotStaleReason staleReason = RefreshReason == RuleRefreshReason.AfterMutation
            ? RuleSnapshotStaleReason.MutationCommitted
            : StaleReason ?? RuleSnapshotStaleReason.RefreshFailed;
        return new(
            RulesPageStatus.Stale,
            Snapshot,
            error,
            refreshReason: null,
            staleReason);
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
            : new RulesPageState(
                RulesPageStatus.Stale,
                Snapshot,
                error,
                refreshReason: null,
                staleReason: staleReason.Value);
    }
}
