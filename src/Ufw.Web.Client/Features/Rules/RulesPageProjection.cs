using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Filtering;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RulesPageProjection(RuleListProjection List, RuleFamilyQueryResult IPv4Query, RuleFamilyQueryResult IPv6Query, bool IPv6Available)
{
    public static RulesPageProjection Empty { get; } = new(
        RuleListProjection.Empty,
        new RuleFamilyQueryResult(FirewallAddressFamily.IPv4, [], 0),
        new RuleFamilyQueryResult(FirewallAddressFamily.IPv6, [], 0),
        IPv6Available: false);
    public RuleFamilyProjection IPv4Family => List.GetFamily(FirewallAddressFamily.IPv4);
    public RuleFamilyProjection IPv6Family => List.GetFamily(FirewallAddressFamily.IPv6);
}
