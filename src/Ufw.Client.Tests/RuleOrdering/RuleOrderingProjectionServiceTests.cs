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
    public void Move_FirstMovePreservesAuthoritativeOriginalPositionsAndRenumbersProjection()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2), Rule("c", 3)];

        RuleOrderingPreview preview = _service.Move(authoritative, null, new RuleMoveRequest("c", 1));

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, preview.Rules.Select(static rule => rule.RuleId).ToArray());
        CollectionAssert.AreEqual(new int?[] { 1, 2, 3 }, preview.Rules.Select(static rule => rule.DisplayNumber).ToArray());
        Assert.AreEqual(1, preview.GetOriginalPosition(authoritative[0]));
        Assert.AreEqual(3, preview.GetOriginalPosition(authoritative[2]));
        Assert.IsTrue(preview.WasDirectlyMoved(authoritative[2]));
        Assert.IsFalse(preview.WasDirectlyMoved(authoritative[0]));
        Assert.HasCount(1, preview.Moves);
    }

    [TestMethod]
    public void Move_SubsequentMoveBuildsOnPreviewWithoutLosingOriginalPositions()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2), Rule("c", 3)];
        RuleOrderingPreview first = _service.Move(authoritative, null, new RuleMoveRequest("c", 1));

        RuleOrderingPreview second = _service.Move(authoritative, first, new RuleMoveRequest("a", 3));

        CollectionAssert.AreEqual(new[] { "c", "b", "a" }, second.Rules.Select(static rule => rule.RuleId).ToArray());
        Assert.AreEqual(1, second.OriginalPositions["a"]);
        Assert.AreEqual(3, second.OriginalPositions["c"]);
        CollectionAssert.AreEquivalent(new[] { "a", "c" }, second.DirectlyMovedRuleIds.ToArray());
        Assert.HasCount(2, second.Moves);
    }

    [TestMethod]
    public void Move_DuplicateStableIdIsRejectedAsAmbiguous()
    {
        ListedFirewallRule[] authoritative = [Rule("dup", 1), Rule("dup", 2)];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest("dup", 1)));
    }

    [TestMethod]
    public void Move_MissingStableIdAndOutOfRangeTargetAreRejected()
    {
        ListedFirewallRule[] authoritative = [Rule("a", 1), Rule("b", 2)];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest("missing", 1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest("a", 0)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            _service.Move(authoritative, null, new RuleMoveRequest("a", 3)));
    }

    [TestMethod]
    public void Move_DuplicateAuthoritativeIdsAreNotRecordedAsOriginalPositionsForOtherMoves()
    {
        ListedFirewallRule[] authoritative = [Rule("dup", 1), Rule("dup", 2), Rule("unique", 3)];

        RuleOrderingPreview preview = _service.Move(authoritative, null, new RuleMoveRequest("unique", 1));

        Assert.IsFalse(preview.OriginalPositions.ContainsKey("dup"));
        Assert.AreEqual(3, preview.OriginalPositions["unique"]);
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
