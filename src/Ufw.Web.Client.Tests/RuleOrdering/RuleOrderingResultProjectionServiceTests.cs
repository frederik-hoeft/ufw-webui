using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Ordering;

namespace Ufw.Web.Client.Tests.RuleOrdering;

[TestClass]
public sealed class RuleOrderingResultProjectionServiceTests
{
    private readonly RuleOrderingResultProjectionService _projection = new();

    [TestMethod]
    public void Create_VirtualizesCombinedIpv6CoordinatesIntoFamilyPositions()
    {
        ListedFirewallRule[] baseline =
        [
            .. Enumerable.Range(0, 8).Select(index => Rule($"v4-{index}", FirewallAddressFamily.IPv4, index + 1)),
            .. Enumerable.Range(0, 4).Select(index => Rule($"v6-{index}", FirewallAddressFamily.IPv6, index + 9)),
        ];
        int[] desiredOrder = [0, 1, 2, 3, 4, 5, 6, 7, 10, 8, 11, 9];
        RuleReorderResponse result = Response(
        [
            Operation(occurrenceId: 9, targetIndex: 11),
            Operation(occurrenceId: 10, targetIndex: 8),
        ]);

        RuleOrderingResultProjection projection = _projection.Create(result, baseline, desiredOrder);

        Assert.HasCount(2, projection.Operations);
        Assert.AreEqual(2, projection.Operations[0].BaselineFamilyPosition);
        Assert.AreEqual(4, projection.Operations[0].TargetFamilyPosition);
        Assert.AreEqual(3, projection.Operations[1].BaselineFamilyPosition);
        Assert.AreEqual(1, projection.Operations[1].TargetFamilyPosition);
    }

    [TestMethod]
    public void Create_DerivesPositionsFromFamilyMembershipRatherThanCombinedIndexLayout()
    {
        ListedFirewallRule[] baseline =
        [
            Rule("v4-a", FirewallAddressFamily.IPv4, 1),
            Rule("v6-a", FirewallAddressFamily.IPv6, 2),
            Rule("v4-b", FirewallAddressFamily.IPv4, 3),
            Rule("v6-b", FirewallAddressFamily.IPv6, 4),
        ];
        int[] desiredOrder = [2, 1, 0, 3];
        RuleReorderMoveResponse pending = new(2, 0, BeforeOccurrenceId: 1);
        RuleReorderMoveResponse blocked = new(0, 2, BeforeOccurrenceId: 3);
        RuleReorderResponse result = new(
            RuleReorderOutcome.PartiallyCompleted,
            FinalSnapshot: null,
            Operations: [],
            BlockedOperations: [blocked],
            PendingOperations: [pending],
            Diagnostic: null);

        RuleOrderingResultProjection projection = _projection.Create(result, baseline, desiredOrder);

        Assert.AreEqual(2, projection.PendingOperations[0].BaselineFamilyPosition);
        Assert.AreEqual(1, projection.PendingOperations[0].TargetFamilyPosition);
        Assert.AreEqual(1, projection.BlockedOperations[0].BaselineFamilyPosition);
        Assert.AreEqual(2, projection.BlockedOperations[0].TargetFamilyPosition);
    }

    [TestMethod]
    public void Create_ResponseTargetMustMatchReviewedDesiredOrder()
    {
        ListedFirewallRule[] baseline =
        [
            Rule("first", FirewallAddressFamily.IPv4, 1),
            Rule("second", FirewallAddressFamily.IPv4, 2),
        ];
        RuleReorderResponse result = Response([Operation(occurrenceId: 1, targetIndex: 1)]);

        Assert.Throws<InvalidOperationException>(() => _projection.Create(result, baseline, [1, 0]));
    }

    private static RuleReorderResponse Response(RuleReorderOperationResponse[] operations) => new(
        RuleReorderOutcome.Completed,
        FinalSnapshot: null,
        operations,
        BlockedOperations: [],
        PendingOperations: [],
        Diagnostic: null);

    private static RuleReorderOperationResponse Operation(int occurrenceId, int targetIndex) => new(
        new RuleReorderMoveResponse(occurrenceId, targetIndex, BeforeOccurrenceId: null),
        RuleReorderOperationOutcome.Applied,
        Diagnostic: null);

    private static ListedFirewallRule Rule(string ruleId, FirewallAddressFamily family, int displayNumber) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification { AddressFamily = family },
    };
}
