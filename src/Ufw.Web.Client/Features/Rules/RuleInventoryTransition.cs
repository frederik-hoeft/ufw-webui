using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules;

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
    public sealed record InsertionCompleted(RuleInsertionResponse Response, DateTimeOffset CapturedAt = default) : RuleInventoryTransition;
    public sealed record ReorderCompleted(RuleReorderResponse Response, DateTimeOffset CapturedAt = default) : RuleInventoryTransition;
    public sealed record MutationFailed(ClientError Error) : RuleInventoryTransition;
}
