using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected occurrence IDs are local one-shot test assertions.")]
public sealed class FirewallReorderPreflightEvaluatorTests
{
    private readonly FirewallReorderPreflightEvaluator _evaluator = new(
        new RuleReorderPlanner(),
        new RuleReinsertabilityClassifier(new UfwRuleCommandRenderer(), new UfwArgumentCountReinsertionCostProvider()));

    [TestMethod]
    public void Evaluate_ReorderableSnapshot_ReturnsPlanWithMoveSpecification()
    {
        RuleListResponse baseline = Snapshot("22", "80", "443");

        RuleReorderPreflightResult result = _evaluator.Evaluate(baseline, [1, 0, 2]);

        RuleReorderPreflight preflight = Assert.IsInstanceOfType<RuleReorderPreflightResult.Accepted>(result).Preflight;
        Assert.HasCount(1, preflight.Plan.Moves);
        Assert.AreEqual(0, preflight.Plan.Moves[0].OccurrenceId);
        Assert.AreEqual("22", preflight.MoveSpecifications[0].DestinationPorts);
        Assert.IsEmpty(preflight.ImmutableOccurrences);
    }

    [TestMethod]
    public void Evaluate_InvalidOccurrencePermutation_ReturnsExplicitPreconditionRejection()
    {
        RuleListResponse baseline = Snapshot("22", "80", "443");

        RuleReorderPreflightResult result = _evaluator.Evaluate(baseline, [0, 0, 2]);

        RuleReorderPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReorderPreflightResult.Rejected>(result);
        StringAssert.Contains(rejected.Diagnostic, "exactly once");
    }

    [TestMethod]
    public void Evaluate_CrossFamilyOrder_ReturnsExplicitPreconditionRejection()
    {
        RuleListResponse baseline = SnapshotTokens("22", "22v6");

        RuleReorderPreflightResult result = _evaluator.Evaluate(baseline, [1, 0]);

        RuleReorderPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReorderPreflightResult.Rejected>(result);
        StringAssert.Contains(rejected.Diagnostic, "IPv4");
    }

    [TestMethod]
    public void Evaluate_ReversingImmutableRows_ReturnsExplicitPreconditionRejection()
    {
        RuleListResponse baseline = new(
            Active: true,
            [
                new ListedFirewallRule { DisplayNumber = 1, Parsed = false, RawLine = "first opaque row" },
                new ListedFirewallRule { DisplayNumber = 2, Parsed = false, RawLine = "second opaque row" },
            ],
            TestFirewallConfiguration.Enabled);

        RuleReorderPreflightResult result = _evaluator.Evaluate(baseline, [1, 0]);

        RuleReorderPreflightResult.Rejected rejected = Assert.IsInstanceOfType<RuleReorderPreflightResult.Rejected>(result);
        Assert.AreEqual("Desired ordering would require moving an immutable occurrence.", rejected.Diagnostic);
    }

    [TestMethod]
    public void CreateSafePendingPlan_AmbiguousDuplicateOccurrences_ReturnsEmptyPlan()
    {
        RuleListResponse baseline = Snapshot("22", "22", "80");
        RuleReorderPreflight preflight = Assert.IsInstanceOfType<RuleReorderPreflightResult.Accepted>(_evaluator.Evaluate(baseline, [2, 0, 1])).Preflight;
        RuleListResponse current = Snapshot("22", "22", "80");

        IReadOnlyList<RuleReorderMove> pending = _evaluator.CreateSafePendingPlan(baseline, current, preflight);

        Assert.IsEmpty(pending);
    }

    [TestMethod]
    public void CreateSafePendingPlan_ImmutableAnchorsChangedOrder_ReturnsEmptyPlan()
    {
        ListedFirewallRule firstOpaque = new() { DisplayNumber = 1, Parsed = false, RawLine = "first opaque row" };
        ListedFirewallRule secondOpaque = new() { DisplayNumber = 3, Parsed = false, RawLine = "second opaque row" };
        ListedFirewallRule parsed = Snapshot("22").Rules[0];
        RuleListResponse baseline = new(true, [firstOpaque, parsed, secondOpaque], TestFirewallConfiguration.Enabled);
        RuleReorderPreflight preflight = Assert.IsInstanceOfType<RuleReorderPreflightResult.Accepted>(_evaluator.Evaluate(baseline, [0, 2, 1])).Preflight;
        RuleListResponse current = new(true, [secondOpaque, parsed, firstOpaque], TestFirewallConfiguration.Enabled);

        IReadOnlyList<RuleReorderMove> pending = _evaluator.CreateSafePendingPlan(baseline, current, preflight);

        Assert.IsEmpty(pending);
    }

    [TestMethod]
    public void CreateSafePendingPlan_UniquePartialState_ReplansFromObservedOrder()
    {
        RuleListResponse baseline = Snapshot("22", "80", "443", "8080");
        RuleReorderPreflight preflight = Assert.IsInstanceOfType<RuleReorderPreflightResult.Accepted>(_evaluator.Evaluate(baseline, [3, 2, 1, 0])).Preflight;
        RuleListResponse current = Snapshot("80", "443", "8080", "22");

        IReadOnlyList<RuleReorderMove> pending = _evaluator.CreateSafePendingPlan(baseline, current, preflight);

        Assert.HasCount(2, pending);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, pending.Select(static move => move.OccurrenceId).ToArray());
    }

    private static RuleListResponse Snapshot(params string[] ports)
    {
        string[] rows = ports.Select(static (port, index) => $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere").ToArray();
        return FirewallRuleSet.ToListResponse(UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!, TestFirewallConfiguration.Enabled);
    }

    private static RuleListResponse SnapshotTokens(params string[] tokens)
    {
        string[] rows = tokens.Select(static (token, index) =>
        {
            bool v6 = token.EndsWith("v6", StringComparison.Ordinal);
            string port = v6 ? token[..^2] : token;
            return v6
                ? $"[ {index + 1}] {port}/tcp (v6)                ALLOW IN    Anywhere (v6)"
                : $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere";
        }).ToArray();
        return FirewallRuleSet.ToListResponse(UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!, TestFirewallConfiguration.Enabled);
    }
}
