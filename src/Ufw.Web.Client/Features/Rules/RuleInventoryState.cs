using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Api;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

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
            RuleInventoryTransition.RefreshCompleted completed => CompleteRefresh(completed.Response),
            RuleInventoryTransition.RefreshFailed failed => FailRefresh(failed.Error),
            RuleInventoryTransition.MetadataMutationCompleted completed => CompleteMetadataMutation(completed.RuleId, completed.Response),
            RuleInventoryTransition.TagCatalogReconciled reconciled => ReconcileTagCatalog(reconciled.Tags),
            RuleInventoryTransition.InsertionCompleted completed => CompleteInsertion(completed.Response),
            RuleInventoryTransition.ReorderCompleted completed => CompleteReorder(completed.Response),
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

    private RuleInventoryState CompleteRefresh(RuleInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!IsLoading)
        {
            throw new InvalidOperationException("A rule inventory refresh cannot complete when no refresh is in progress.");
        }

        return new RuleInventoryState(RuleInventoryStatus.Current, RuleSnapshot.FromResponse(response), error: null, refreshReason: null, staleReason: null);
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

        return new RuleInventoryState(Status, Snapshot.ApplyMetadataMutation(ruleId, response), Error, RefreshReason, StaleReason);
    }

    private RuleInventoryState ReconcileTagCatalog(IReadOnlyList<RuleTag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return Snapshot is null ? this : new RuleInventoryState(Status, Snapshot.ReconcileTagCatalog(tags), Error, RefreshReason, StaleReason);
    }

    private RuleInventoryState CompleteInsertion(RuleInsertionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return CompleteMutation(response.FinalSnapshot, "An insertion response cannot replace an unloaded rule snapshot.");
    }

    private RuleInventoryState CompleteReorder(RuleReorderResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return CompleteMutation(response.FinalSnapshot, "A reorder response cannot replace an unloaded rule snapshot.");
    }

    private RuleInventoryState CompleteMutation(RuleListResponse? finalSnapshot, string unloadedMessage)
    {
        if (finalSnapshot is not null)
        {
            RuleSnapshot snapshot = RuleSnapshot.FromFirewallResponse(finalSnapshot, Snapshot?.Metadata);
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
