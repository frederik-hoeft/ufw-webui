using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Client.Rules.Metadata;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Rules;

internal abstract record RuleInventoryTransition
{
    private RuleInventoryTransition()
    {
    }

    public sealed record RefreshStarted(RuleInventoryRefreshReason Reason) : RuleInventoryTransition;
    public sealed record RefreshCompleted(RuleInventoryResponse Response) : RuleInventoryTransition;
    public sealed record RefreshFailed(ClientError Error) : RuleInventoryTransition;
    public sealed record MetadataMutationCompleted(string RuleId, RuleMetadataMutationResponse Response) : RuleInventoryTransition;
    public sealed record TagCatalogReconciled(IReadOnlyList<RuleTag> Tags) : RuleInventoryTransition;
    public sealed record InsertionCompleted(RuleInsertionResponse Response) : RuleInventoryTransition;
    public sealed record ReorderCompleted(RuleReorderResponse Response) : RuleInventoryTransition;
    public sealed record MutationFailed(ClientError Error) : RuleInventoryTransition;
}
