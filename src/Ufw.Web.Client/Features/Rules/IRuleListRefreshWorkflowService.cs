using Ufw.Shared.Management.KnownHosts;

namespace Ufw.Web.Client.Features.Rules;

internal interface IRuleListRefreshWorkflowService
{
    Task<RuleListRefreshResult> RefreshAsync(
        RuleInventoryState refreshingState,
        IReadOnlyList<KnownHostInventoryItem> previousKnownHosts,
        CancellationToken cancellationToken = default);
}
