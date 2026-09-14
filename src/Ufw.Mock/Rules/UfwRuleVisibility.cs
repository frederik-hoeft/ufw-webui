using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Rules;

internal static class UfwRuleVisibility
{
    public static IReadOnlyList<UfwMockRule> GetObservableRules(UfwMockState state, bool ipv6Enabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ipv6Enabled
            ? state.Rules
            : state.Rules.Where(static rule => rule.Specification.AddressFamily != FirewallAddressFamily.IPv6).ToArray();
    }
}
