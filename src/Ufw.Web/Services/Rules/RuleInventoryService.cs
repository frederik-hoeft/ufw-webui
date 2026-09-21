using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleInventoryService(IDaemonRuleSource daemonRules, IRuleMetadataRepository metadata) : IRuleInventoryService
{
    public async Task<RuleInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        RuleListResponse firewall = await daemonRules.GetAsync(cancellationToken);
        string[] ruleIds = [.. firewall.Rules
            .Select(static rule => rule.RuleId)
            .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)];
        IReadOnlyList<RuleMetadataItem> enrichment = await metadata.GetForRuleIdsAsync(ruleIds, cancellationToken);
        return new RuleInventoryResponse { Firewall = firewall, Metadata = enrichment };
    }
}
