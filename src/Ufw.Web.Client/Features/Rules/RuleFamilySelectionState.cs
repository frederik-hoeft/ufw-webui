using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleFamilySelectionState(FirewallAddressFamily SelectedFamily)
{
    public static RuleFamilySelectionState Initial { get; } = new(FirewallAddressFamily.IPv4);

    public static bool IsIPv6Available(bool capabilityEnabled, int observedRuleCount) =>
        capabilityEnabled || observedRuleCount > 0;

    public RuleFamilySelectionState Select(FirewallAddressFamily requestedFamily, bool ipv6Available)
    {
        if (requestedFamily == FirewallAddressFamily.IPv6 && !ipv6Available)
        {
            return this;
        }

        return requestedFamily == SelectedFamily
            ? this
            : new RuleFamilySelectionState(requestedFamily);
    }

    public RuleFamilySelectionState Reconcile(bool ipv6Available) =>
        SelectedFamily == FirewallAddressFamily.IPv6 && !ipv6Available
            ? Initial
            : this;
}
