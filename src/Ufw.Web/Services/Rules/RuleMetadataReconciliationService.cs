using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleMetadataReconciliationService(IRuleDaemonGateway daemonRules, IRuleMetadataDataAccess metadata) : IRuleMetadataReconciliationService
{
    public async Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        DaemonResult<RuleListResponse> daemonResult = await daemonRules.GetRulesAsync(cancellationToken);
        RuleListResponse snapshot = daemonResult.Result;
        IReadOnlyList<RuleMetadataItem> items = await metadata.GetAllAsync(cancellationToken);
        return BuildResponse(snapshot, items, removedCount: 0);
    }

    public async Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid[] selectedIds = [.. request.MetadataIds.Distinct().Order()];
        DaemonResult<RuleListResponse> daemonResult = await daemonRules.GetRulesAsync(cancellationToken);
        RuleListResponse snapshot = daemonResult.Result;
        LiveRuleIdentitySet liveRuleIds = LiveRuleIdentitySet.FromSnapshot(snapshot);
        int removedCount = await metadata.DeleteUnmatchedAsync(selectedIds, liveRuleIds, cancellationToken);
        IReadOnlyList<RuleMetadataItem> items = await metadata.GetAllAsync(cancellationToken);
        return BuildResponse(snapshot, items, removedCount);
    }

    private static RuleMetadataReconciliationResponse BuildResponse(RuleListResponse snapshot, IReadOnlyList<RuleMetadataItem> metadata, int removedCount)
    {
        LiveRuleIdentitySet liveRuleIds = LiveRuleIdentitySet.FromSnapshot(snapshot);
        RuleMetadataItem[] orphans = [.. metadata
            .Where(item => !liveRuleIds.Contains(item.RuleId))
            .OrderBy(static item => item.RuleId, StringComparer.Ordinal)
            .ThenBy(static item => item.Id)];
        return new RuleMetadataReconciliationResponse { Orphans = orphans, RemovedCount = removedCount };
    }
}
