using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleSnapshotMatcherTests
{
    [TestMethod]
    public void MatchesOrder_UsesBaselineOccurrencesInRequestedOrder()
    {
        RuleListResponse baseline = Response(Rule("22"), Rule("80"), Rule("443"));
        RuleListResponse reordered = Response(Rule("443"), Rule("22"));

        Assert.IsTrue(FirewallRuleSnapshotMatcher.MatchesOrder(reordered, baseline, [2, 0]));
        Assert.IsFalse(FirewallRuleSnapshotMatcher.MatchesOrder(reordered, baseline, [0, 2]));
    }

    [TestMethod]
    public void CountMatches_UsesNormalizedObservedStateEquality()
    {
        FirewallRuleSpecification specification = Rule("22").Rule!;
        IReadOnlyList<ListedFirewallRule> rules = [Rule("22"), Rule("80"), Rule("22")];

        int count = FirewallRuleSnapshotMatcher.CountMatches(rules, specification);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void MatchesOrder_RejectsDifferentActivityAndInvalidOccurrences()
    {
        RuleListResponse baseline = Response(Rule("22"));
        RuleListResponse inactive = new(Active: false, baseline.Rules, TestFirewallConfiguration.Enabled);

        Assert.IsFalse(FirewallRuleSnapshotMatcher.MatchesOrder(inactive, baseline, [0]));
        Assert.IsFalse(FirewallRuleSnapshotMatcher.MatchesOrder(baseline, baseline, [1]));
    }

    private static RuleListResponse Response(params ListedFirewallRule[] rules) => new(Active: true, rules, TestFirewallConfiguration.Enabled);

    private static ListedFirewallRule Rule(string port) => new()
    {
        Parsed = true,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = port,
        },
    };
}
