using Ufw.Client.Api;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Services.Rules;

internal sealed class RulesPageProjectionService(IRuleListProjectionService listProjection, IRuleQueryService queryService) : IRulesPageProjectionService
{
    public RulesPageProjection Create(RuleSnapshot? snapshot, RuleOrderingPreview? orderingPreview, RuleQuery query, IReadOnlyList<KnownHostInventoryItem> knownHosts)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(knownHosts);

        RuleListProjection list = listProjection.Create(snapshot?.Rules ?? [], orderingPreview, snapshot?.Metadata);
        RuleFamilyProjection ipv4 = list.GetFamily(FirewallAddressFamily.IPv4);
        RuleFamilyProjection ipv6 = list.GetFamily(FirewallAddressFamily.IPv6);
        bool ipv6Available = snapshot is not null && RuleFamilySelectionState.IsIPv6Available(snapshot.Configuration.IPv6Enabled, ipv6.Rows.Count);
        return new RulesPageProjection(list, queryService.Evaluate(ipv4, query, knownHosts), queryService.Evaluate(ipv6, query, knownHosts), ipv6Available);
    }
}
