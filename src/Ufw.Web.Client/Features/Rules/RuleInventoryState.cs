using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleInventoryState
{
    private RuleInventoryState(RuleInventoryStatus status, RuleSnapshot? snapshot, ClientError? error, RuleInventoryRefreshReason? refreshReason, RuleSnapshotStaleReason? staleReason)
    {
        Status = status;
        Snapshot = snapshot;
        Error = error;
        RefreshReason = refreshReason;
        StaleReason = staleReason;
    }

    public RuleInventoryStatus Status { get; }
    public RuleSnapshot? Snapshot { get; }
    public ClientError? Error { get; }
    public RuleInventoryRefreshReason? RefreshReason { get; }
    public RuleSnapshotStaleReason? StaleReason { get; }
    public bool IsLoading => Status is RuleInventoryStatus.Loading or RuleInventoryStatus.Refreshing;
    public bool IsCurrent => Status == RuleInventoryStatus.Current;
    public bool IsStale => Status == RuleInventoryStatus.Stale;
    public static RuleInventoryState Initial { get; } = new(RuleInventoryStatus.NotLoaded, snapshot: null, error: null, refreshReason: null, staleReason: null);

    public RuleInventoryState MoveNext(RuleInventoryTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return transition switch
        {
            RuleInventoryTransition.RefreshStarted started => BeginRefresh(started.Reason),
            RuleInventoryTransition.RefreshCompleted completed => CompleteRefresh(completed.Snapshot),
            RuleInventoryTransition.RefreshFailed failed => FailRefresh(failed.Error),
            RuleInventoryTransition.MetadataMutationCompleted completed => CompleteMetadataMutation(completed.RuleId, completed.Response),
            RuleInventoryTransition.TagCatalogReconciled reconciled => ReconcileTagCatalog(reconciled.Tags),
            RuleInventoryTransition.GroupCatalogReconciled reconciled => ReconcileGroupCatalog(reconciled.Groups),
            RuleInventoryTransition.InsertionCompleted completed => CompleteInsertion(completed.Response, completed.CapturedAt),
            RuleInventoryTransition.ReplacementCompleted completed => CompleteReplacement(completed.Response, completed.CapturedAt),
            RuleInventoryTransition.ReorderCompleted completed => CompleteReorder(completed.Response, completed.CapturedAt),
            RuleInventoryTransition.MutationFailed failed => FailMutation(failed.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
        };
    }

    private RuleInventoryState BeginRefresh(RuleInventoryRefreshReason reason)
    {
        if (IsLoading)
        {
            throw new InvalidOperationException("A rule inventory refresh is already in progress.");
        }

        return new RuleInventoryState(Snapshot is null ? RuleInventoryStatus.Loading : RuleInventoryStatus.Refreshing, Snapshot, error: null, refreshReason: reason, staleReason: StaleReason);
    }

    private RuleInventoryState CompleteRefresh(RuleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!IsLoading)
        {
            throw new InvalidOperationException("A rule inventory refresh cannot complete when no refresh is in progress.");
        }

        return new RuleInventoryState(RuleInventoryStatus.Current, snapshot, error: null, refreshReason: null, staleReason: null);
    }

    private RuleInventoryState FailRefresh(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!IsLoading)
        {
            throw new InvalidOperationException("A rule inventory refresh cannot fail when no refresh is in progress.");
        }
        if (Snapshot is null)
        {
            return new RuleInventoryState(RuleInventoryStatus.Failed, snapshot: null, error, refreshReason: null, staleReason: null);
        }

        RuleSnapshotStaleReason staleReason = RefreshReason == RuleInventoryRefreshReason.AfterMutation
            ? RuleSnapshotStaleReason.MutationCommitted
            : StaleReason ?? RuleSnapshotStaleReason.RefreshFailed;
        return new RuleInventoryState(RuleInventoryStatus.Stale, Snapshot, error, refreshReason: null, staleReason);
    }

    private RuleInventoryState CompleteMetadataMutation(string ruleId, RuleMetadataMutationResponse response)
    {
        if (Snapshot is null)
        {
            throw new InvalidOperationException("Rule metadata cannot be updated against an unloaded rule snapshot.");
        }

        return new RuleInventoryState(Status, RuleSnapshotFactory.ApplyMetadataMutation(Snapshot, ruleId, response), Error, RefreshReason, StaleReason);
    }

    private RuleInventoryState ReconcileTagCatalog(IReadOnlyList<RuleTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return Snapshot is null ? this : new RuleInventoryState(Status, Snapshot.ReconcileTagCatalog(tags), Error, RefreshReason, StaleReason);
    }

    private RuleInventoryState ReconcileGroupCatalog(IReadOnlyList<RuleGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return Snapshot is null ? this : new RuleInventoryState(Status, Snapshot.ReconcileGroupCatalog(groups), Error, RefreshReason, StaleReason);
    }

    private RuleInventoryState CompleteInsertion(RuleInsertionResponse response, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        return CompleteMutation(response.FinalSnapshot, capturedAt, "An insertion response cannot replace an unloaded rule snapshot.");
    }

    private RuleInventoryState CompleteReplacement(RuleReplacementResponse response, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        return CompleteMutation(response.FinalSnapshot, capturedAt, "A replacement response cannot replace an unloaded rule snapshot.");
    }

    private RuleInventoryState CompleteReorder(RuleReorderResponse response, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        return CompleteMutation(response.FinalSnapshot, capturedAt, "A reorder response cannot replace an unloaded rule snapshot.");
    }

    private RuleInventoryState CompleteMutation(RuleListResponse? finalSnapshot, DateTimeOffset capturedAt, string unloadedMessage)
    {
        if (finalSnapshot is not null)
        {
            RuleSnapshot snapshot = RuleSnapshotFactory.FromFirewallResponse(finalSnapshot, Snapshot?.Metadata, capturedAt == default ? Snapshot?.CapturedAt ?? default : capturedAt);
            return new RuleInventoryState(RuleInventoryStatus.Current, snapshot, error: null, refreshReason: null, staleReason: null);
        }
        if (Snapshot is null)
        {
            throw new InvalidOperationException(unloadedMessage);
        }

        return new RuleInventoryState(RuleInventoryStatus.Stale, Snapshot, error: null, refreshReason: null, staleReason: RuleSnapshotStaleReason.MutationOutcomeUnknown);
    }

    private RuleInventoryState FailMutation(ClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (Snapshot is null)
        {
            throw new InvalidOperationException("A mutation cannot fail against an unloaded rule snapshot.");
        }

        RuleSnapshotStaleReason? staleReason = error.Kind switch
        {
            ClientErrorKind.Unavailable or ClientErrorKind.Protocol or ClientErrorKind.Canceled => RuleSnapshotStaleReason.MutationOutcomeUnknown,
            ClientErrorKind.Conflict => RuleSnapshotStaleReason.MutationRejectedRequiresRefresh,
            ClientErrorKind.RequestRejected when error.Retryable => RuleSnapshotStaleReason.MutationRejectedRequiresRefresh,
            _ => null,
        };
        return staleReason is null ? this : new RuleInventoryState(RuleInventoryStatus.Stale, Snapshot, error, refreshReason: null, staleReason: staleReason.Value);
    }
}
