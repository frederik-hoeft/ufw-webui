using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleInventoryService(IRuleDaemonGateway daemonRules, IRuleMetadataDataAccess metadata, TimeProvider timeProvider) : IRuleInventoryService
{
    public async Task<RuleInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        DaemonResult<RuleListResponse> daemonResult = await daemonRules.GetRulesAsync(cancellationToken);
        RuleListResponse firewall = daemonResult.Result;
        DateTimeOffset capturedAt = timeProvider.GetUtcNow();
        LiveRuleIdentitySet liveRuleIds = LiveRuleIdentitySet.FromSnapshot(firewall);
        IReadOnlyList<RuleMetadataItem> enrichment = await metadata.GetForRuleIdsAsync(liveRuleIds, cancellationToken);
        return new RuleInventoryResponse { Firewall = firewall, Metadata = enrichment, CapturedAt = capturedAt };
    }
}
