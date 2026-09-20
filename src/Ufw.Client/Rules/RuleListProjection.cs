using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

internal sealed record RuleListProjection(IReadOnlyList<RuleFamilyProjection> Families)
{
    public static RuleListProjection Empty { get; } = new([new RuleFamilyProjection(FirewallAddressFamily.IPv4, []), new RuleFamilyProjection(FirewallAddressFamily.IPv6, []),]);

    public RuleFamilyProjection GetFamily(FirewallAddressFamily addressFamily) =>
        Families.Single(family => family.AddressFamily == addressFamily);
}
