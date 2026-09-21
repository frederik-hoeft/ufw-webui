using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Rules;

[TestClass]
public sealed class RuleFamilySelectionStateTests
{
    [TestMethod]
    [DataRow(false, 0, false)]
    [DataRow(true, 0, true)]
    [DataRow(false, 1, true)]
    public void IsIPv6Available_UsesCapabilityOrObservedRules(bool capabilityEnabled, int observedRuleCount, bool expected)
    {
        Assert.AreEqual(expected, RuleFamilySelectionState.IsIPv6Available(capabilityEnabled, observedRuleCount));
    }

    [TestMethod]
    public void Initial_SelectsIPv4()
    {
        Assert.AreEqual(FirewallAddressFamily.IPv4, RuleFamilySelectionState.Initial.SelectedFamily);
    }

    [TestMethod]
    public void Select_IPv6Available_SelectsIPv6()
    {
        RuleFamilySelectionState state = RuleFamilySelectionState.Initial.Select(FirewallAddressFamily.IPv6, ipv6Available: true);

        Assert.AreEqual(FirewallAddressFamily.IPv6, state.SelectedFamily);
    }

    [TestMethod]
    public void Select_IPv6Unavailable_KeepsCurrentSelection()
    {
        RuleFamilySelectionState state = RuleFamilySelectionState.Initial.Select(FirewallAddressFamily.IPv6, ipv6Available: false);

        Assert.AreSame(RuleFamilySelectionState.Initial, state);
    }

    [TestMethod]
    public void Reconcile_SelectedIPv6BecomesUnavailable_FallsBackToIPv4()
    {
        RuleFamilySelectionState selected = RuleFamilySelectionState.Initial.Select(FirewallAddressFamily.IPv6, ipv6Available: true);

        RuleFamilySelectionState reconciled = selected.Reconcile(ipv6Available: false);

        Assert.AreSame(RuleFamilySelectionState.Initial, reconciled);
    }

    [TestMethod]
    public void Reconcile_SelectedIPv6RemainsAvailable_PreservesSelection()
    {
        RuleFamilySelectionState selected = RuleFamilySelectionState.Initial.Select(FirewallAddressFamily.IPv6, ipv6Available: true);

        RuleFamilySelectionState reconciled = selected.Reconcile(ipv6Available: true);

        Assert.AreSame(selected, reconciled);
    }
}
