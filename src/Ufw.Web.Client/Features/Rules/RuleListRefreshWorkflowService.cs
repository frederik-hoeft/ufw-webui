using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules;

internal sealed class RuleListRefreshWorkflowService(
    IRuleInventoryService inventory,
    IKnownHostInventoryService knownHosts,
    IClientErrorMapper clientErrors) : IRuleListRefreshWorkflowService
{
    public async Task<RuleListRefreshResult> RefreshAsync(
        RuleInventoryState refreshingState,
        IReadOnlyList<KnownHostInventoryItem> previousKnownHosts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshingState);
        ArgumentNullException.ThrowIfNull(previousKnownHosts);
        if (!refreshingState.IsLoading)
        {
            throw new InvalidOperationException("A rule-list refresh must begin before loading the authoritative snapshot.");
        }

        RuleSnapshot snapshot;
        try
        {
            snapshot = await inventory.GetAsync(cancellationToken);
        }
        catch (Exception exception) when (clientErrors.CanDescribe(exception))
        {
            return new RuleListRefreshResult(refreshingState.MoveNext(new RuleInventoryTransition.RefreshFailed(clientErrors.Describe(exception))), previousKnownHosts);
        }

        RuleInventoryState current = refreshingState.MoveNext(new RuleInventoryTransition.RefreshCompleted(snapshot));
        try
        {
            KnownHostInventoryResponse response = await knownHosts.RefreshAsync(cancellationToken);
            return new RuleListRefreshResult(current, VisibleHosts(response));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (clientErrors.CanDescribe(exception))
        {
            return new RuleListRefreshResult(current, knownHosts.Current is { } cached ? VisibleHosts(cached) : []);
        }
    }

    private static IReadOnlyList<KnownHostInventoryItem> VisibleHosts(KnownHostInventoryResponse response) =>
        response.Hosts.Where(static host => host.IsVisible).ToArray();
}
