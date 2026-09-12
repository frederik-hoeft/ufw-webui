using Ufw.Client.Components.Rules;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Tests.Components.Rules;

[TestClass]
public sealed class RulesPageStateTests
{
    [TestMethod]
    public void AfterReorder_WithAuthoritativeFinalSnapshotReplacesLocalAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(new RuleListResponse(true, [Rule("old")]));
        RuleListResponse finalSnapshot = new(false, [Rule("new")]);
        RuleReorderResponse report = new(
            RuleReorderOutcome.PartiallyCompleted,
            finalSnapshot,
            [],
            [],
            [],
            Diagnostic: "partial");

        RulesPageState updated = state.AfterReorder(report);

        Assert.IsTrue(updated.IsCurrent);
        Assert.IsNotNull(updated.Snapshot);
        Assert.IsFalse(updated.Snapshot.FirewallActive);
        Assert.AreEqual("new", updated.Snapshot.Rules[0].RuleId);
        Assert.IsNull(updated.StaleReason);
    }

    [TestMethod]
    public void AfterReorder_WithoutReadableFinalSnapshotInvalidatesExistingAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(new RuleListResponse(true, [Rule("old")]));
        RuleReorderResponse report = new(
            RuleReorderOutcome.StateUncertain,
            FinalSnapshot: null,
            [],
            [],
            [],
            Diagnostic: "unreadable");

        RulesPageState updated = state.AfterReorder(report);

        Assert.IsTrue(updated.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, updated.StaleReason);
        Assert.AreEqual("old", updated.Snapshot!.Rules[0].RuleId);
    }

    private static ListedFirewallRule Rule(string id) => new()
    {
        RuleId = id,
        DisplayNumber = 1,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification(),
    };
}
