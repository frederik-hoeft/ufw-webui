using Ufw.Client.Api;
using Ufw.Client.Localization;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Tests.Support;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.RuleOrdering;

[TestClass]
public sealed class RuleOrderingProjectionServiceTests
{
    private readonly RuleOrderingProjectionService _service = new(new PassthroughStringLocalizer<RulesStrings>());

    [TestMethod]
    public void Move_FirstMoveUsesBaselineOccurrenceIdsAndRenumbersProjection()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2), Rule("c", 3)];

        RuleOrderingPreview preview = _service.Move(authoritative, null, new RuleMoveRequest(2, 1));

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, preview.Rules.Select(static rule => rule.RuleId).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 0, 1 }, preview.DesiredOrder.ToArray());
        CollectionAssert.AreEqual(new int?[] { 1, 2, 3 }, preview.Rules.Select(static rule => rule.DisplayNumber).ToArray());
        Assert.AreEqual(3, preview.GetOriginalPosition(preview.Rules[0]));
        Assert.AreEqual(1, preview.GetOriginalPosition(preview.Rules[1]));
        Assert.IsTrue(preview.WasDirectlyMoved(preview.Rules[0]));
        Assert.IsFalse(preview.WasDirectlyMoved(preview.Rules[1]));
        Assert.IsTrue(preview.HasChanges);
    }

    [TestMethod]
    public void Move_SubsequentMoveBuildsOnOccurrencePermutation()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2), Rule("c", 3)];
        RuleOrderingPreview first = _service.Move(authoritative, null, new RuleMoveRequest(2, 1));

        RuleOrderingPreview second = _service.Move(authoritative, first, new RuleMoveRequest(0, 3));

        CollectionAssert.AreEqual(new[] { "c", "b", "a" }, second.Rules.Select(static rule => rule.RuleId).ToArray());
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, second.DesiredOrder.ToArray());
        CollectionAssert.AreEquivalent(new[] { 0, 2 }, second.DirectlyMovedOccurrences.ToArray());
    }

    [TestMethod]
    public void Move_DuplicateSemanticRuleIdsRemainDistinctOccurrences()
    {
        ListedFirewallRule first = Rule("dup", 1);
        ListedFirewallRule second = Rule("dup", 2);
        ListedFirewallRule[] authoritative = [first, second];

        RuleOrderingPreview preview = _service.Move(authoritative, null, new RuleMoveRequest(1, 1));

        CollectionAssert.AreEqual(new[] { 1, 0 }, preview.DesiredOrder.ToArray());
        Assert.AreEqual(2, preview.GetOriginalPosition(preview.Rules[0]));
        Assert.AreEqual(1, preview.GetOriginalPosition(preview.Rules[1]));
        Assert.IsTrue(preview.WasDirectlyMoved(preview.Rules[0]));
    }

    [TestMethod]
    public void Move_InvalidOccurrenceAndOutOfRangeTargetAreRejected()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2)];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest(2, 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest(0, 0)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest(0, 3)));
    }

    [TestMethod]
    public void Move_ReturningToBaselineProducesUnchangedPreview()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2)];
        RuleOrderingPreview first = _service.Move(authoritative, null, new RuleMoveRequest(1, 1));

        RuleOrderingPreview second = _service.Move(authoritative, first, new RuleMoveRequest(1, 2));

        CollectionAssert.AreEqual(new[] { 0, 1 }, second.DesiredOrder.ToArray());
        Assert.IsFalse(second.HasChanges);
    }

    private static ListedFirewallRule Rule(string id, int number) => new()
    {
        RuleId = id,
        DisplayNumber = number,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification(),
    };
}
