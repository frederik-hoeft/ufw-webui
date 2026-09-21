using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleMutationReconciliationServiceTests
{
    private readonly RuleMutationReconciliationService _service = new();

    [TestMethod]
    public void GetMutationIdentity_PrefersAuthoritativeSemanticIdentity()
    {
        FirewallRuleSpecification requested = Specification(FirewallAddressFamily.Any);
        string fallback = _service.GetRequestedIdentity(requested);
        ListedFirewallRule concrete = Rule("authoritative", FirewallAddressFamily.IPv4);
        RuleMutationResponse response = new("add", concrete);

        string identity = _service.GetMutationIdentity(response, fallback);

        Assert.AreEqual("authoritative", identity);
    }

    [TestMethod]
    public void IsPresent_FallsBackToComputedIdentityForParsedRowsWithoutRuleId()
    {
        FirewallRuleSpecification specification = Specification(FirewallAddressFamily.IPv4);
        string identity = RuleIdentity.Compute(specification);
        RuleSnapshot snapshot = new(true, [new ListedFirewallRule { Parsed = true, RawLine = "rule", Rule = specification }], TestFirewallConfiguration.Enabled);

        Assert.IsTrue(_service.IsPresent(snapshot, identity));
    }

    [TestMethod]
    public void IsPresent_DoesNotTreatOpaqueRowsAsConfirmedMutationResults()
    {
        ListedFirewallRule opaque = new()
        {
            RuleId = "opaque-id",
            Parsed = false,
            RawLine = "opaque rule",
        };
        RuleSnapshot snapshot = new(true, [opaque], TestFirewallConfiguration.Enabled);

        Assert.IsFalse(_service.IsPresent(snapshot, "opaque-id"));
    }

    [TestMethod]
    public void MustReselectInsertionAnchor_RequiresSameAuthoritativeBaseline()
    {
        RuleListResponse baseline = new(true, [Rule("anchor", FirewallAddressFamily.IPv4)], TestFirewallConfiguration.Enabled);
        OrderedRuleInsertionNavigationContext context = new(
            FirewallRuleSnapshotFingerprint.Compute(baseline),
            0,
            1,
            RuleInsertionPlacement.After,
            FirewallAddressFamily.IPv4,
            baseline.Rules[0]);
        RuleInsertionResponse completed = new(RuleInsertionOutcome.Completed, baseline, InsertedRule: null, Diagnostic: null);
        RuleListResponse changed = new(true, [Rule("different", FirewallAddressFamily.IPv4)], TestFirewallConfiguration.Enabled);
        RuleInsertionResponse stale = new(RuleInsertionOutcome.PreconditionFailed, changed, InsertedRule: null, Diagnostic: null);

        Assert.IsFalse(_service.MustReselectInsertionAnchor(completed, context));
        Assert.IsTrue(_service.MustReselectInsertionAnchor(stale, context));
    }

    [TestMethod]
    public void MustReselectInsertionAnchor_StateUncertainAlwaysRequiresReselection()
    {
        RuleListResponse baseline = new(true, [Rule("anchor", FirewallAddressFamily.IPv4)], TestFirewallConfiguration.Enabled);
        OrderedRuleInsertionNavigationContext context = new(
            FirewallRuleSnapshotFingerprint.Compute(baseline),
            0,
            1,
            RuleInsertionPlacement.Before,
            FirewallAddressFamily.IPv4,
            baseline.Rules[0]);
        RuleInsertionResponse response = new(RuleInsertionOutcome.StateUncertain, baseline, InsertedRule: null, Diagnostic: null);

        Assert.IsTrue(_service.MustReselectInsertionAnchor(response, context));
    }

    private static ListedFirewallRule Rule(string id, FirewallAddressFamily family) => new()
    {
        RuleId = id,
        Parsed = true,
        DisplayNumber = 1,
        RawLine = id,
        Rule = Specification(family),
    };

    private static FirewallRuleSpecification Specification(FirewallAddressFamily family) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
    };
}
