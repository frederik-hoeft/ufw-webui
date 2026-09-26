using System.Net;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleGroupDeletionWorkflowServiceTests
{
    [TestMethod]
    public async Task GetSingleRuleCleanupCandidateAsync_ReturnsSoleGroupMemberWithUniqueLiveOccurrenceAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["rule"]);
        RuleSnapshot snapshot = Snapshot([Rule("rule")], new Dictionary<string, RuleMetadata>(StringComparer.Ordinal)
        {
            ["rule"] = Metadata(group),
        });
        host.Groups.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync([group]);

        RuleGroup? candidate = await host.Service.GetSingleRuleCleanupCandidateAsync(snapshot.Rules[0], snapshot);

        Assert.AreSame(group, candidate);
    }

    [TestMethod]
    public async Task GetSingleRuleCleanupCandidateAsync_DuplicateSemanticOccurrenceSuppressesCleanupOptionAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["rule"]);
        RuleSnapshot snapshot = Snapshot([Rule("rule"), Rule("rule")], new Dictionary<string, RuleMetadata>(StringComparer.Ordinal)
        {
            ["rule"] = Metadata(group),
        });

        RuleGroup? candidate = await host.Service.GetSingleRuleCleanupCandidateAsync(snapshot.Rules[0], snapshot);

        Assert.IsNull(candidate);
        host.Groups.VerifyNoOtherCalls();
        host.Mutations.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteAsync_CompletedBatchDeletesEveryOccurrenceThenEmptyGroupAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one", "two"]);
        RuleSnapshot snapshot = Snapshot([Rule("one"), Rule("one"), Rule("two")]);
        RuleGroupManagementProjection projection = Projection(group,
            Member("one", Row(snapshot.Rules[0], occurrenceId: 0), Row(snapshot.Rules[1], occurrenceId: 1)),
            Member("two", Row(snapshot.Rules[2], occurrenceId: 2)));
        RuleListResponse finalSnapshot = new(true, [], TestFirewallConfiguration.Enabled);
        RuleBatchDeleteResponse response = new(RuleBatchDeleteOutcome.Completed, finalSnapshot, [], [], Diagnostic: null);
        RuleGroup emptyGroup = group with { RuleIds = [] };
        host.Mutations.Setup(service => service.BatchDeleteRulesAsync(
                It.Is<RuleListResponse>(baseline => baseline.Rules.SequenceEqual(snapshot.Rules)),
                It.Is<IReadOnlyList<int>>(ids => ids.SequenceEqual(new[] { 0, 1, 2 })),
                "key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([group])
            .ReturnsAsync([emptyGroup]);
        host.Groups.Setup(service => service.DeleteAsync(group.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot, "key");

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.Deleted, result.Outcome);
        Assert.AreSame(response, result.BatchResponse);
        Assert.AreEqual(0, result.Groups.Count);
    }

    [TestMethod]
    public async Task DeleteAsync_MembershipChangedAfterConfirmationRejectsBeforeMutationAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one"]);
        RuleGroup changed = group with { RuleIds = ["one", "two"] };
        RuleSnapshot snapshot = Snapshot([Rule("one")]);
        RuleGroupManagementProjection projection = Projection(group, Member("one", Row(snapshot.Rules[0], occurrenceId: 0)));
        host.Groups.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync([changed]);

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot, "key");

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.GroupChanged, result.Outcome);
        CollectionAssert.AreEqual(new[] { changed }, result.Groups.ToArray());
        host.Mutations.VerifyNoOtherCalls();
        host.Groups.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_IncompleteConfirmationProjectionRejectsBeforeMutationAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one"]);
        RuleSnapshot snapshot = Snapshot([Rule("one"), Rule("one")]);
        RuleGroupManagementProjection projection = Projection(group, Member("one", Row(snapshot.Rules[0], occurrenceId: 0)));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.Service.DeleteAsync(projection, snapshot, "key"));

        StringAssert.Contains(exception.Message, "preview");
        host.Mutations.VerifyNoOtherCalls();
        host.Groups.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteAsync_CompletedBatchRetainsGroupWhenMetadataStillReferencesItAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one"]);
        RuleSnapshot snapshot = Snapshot([Rule("one")]);
        RuleGroupManagementProjection projection = Projection(group, Member("one", Row(snapshot.Rules[0], occurrenceId: 0)));
        RuleBatchDeleteResponse response = new(RuleBatchDeleteOutcome.Completed, new RuleListResponse(true, [], TestFirewallConfiguration.Enabled), [], [], Diagnostic: null);
        host.Mutations.Setup(service => service.BatchDeleteRulesAsync(It.IsAny<RuleListResponse>(), It.IsAny<IReadOnlyList<int>>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(response);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([group])
            .ReturnsAsync([group]);

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot, "key");

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.GroupRetained, result.Outcome);
        CollectionAssert.AreEqual(new[] { group }, result.Groups.ToArray());
        host.Groups.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_CompletedBatchPreservesOutcomeWhenGroupRefreshFailsAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one"]);
        RuleSnapshot snapshot = Snapshot([Rule("one")]);
        RuleGroupManagementProjection projection = Projection(group, Member("one", Row(snapshot.Rules[0], occurrenceId: 0)));
        RuleBatchDeleteResponse response = new(RuleBatchDeleteOutcome.Completed, new RuleListResponse(true, [], TestFirewallConfiguration.Enabled), [], [], Diagnostic: null);
        host.Mutations.Setup(service => service.BatchDeleteRulesAsync(It.IsAny<RuleListResponse>(), It.IsAny<IReadOnlyList<int>>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(response);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([group])
            .ThrowsAsync(new ApiRequestException(HttpStatusCode.ServiceUnavailable, "groups unavailable"));
        host.Groups.SetupGet(service => service.Current).Returns([group]);

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot, "key");

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.GroupCleanupFailed, result.Outcome);
        Assert.AreSame(response, result.BatchResponse);
        Assert.AreEqual("groups unavailable", result.CleanupDiagnostic);
        CollectionAssert.AreEqual(new[] { group }, result.Groups.ToArray());
    }

    [TestMethod]
    public async Task DeleteAsync_PartialBatchNeverDeletesGroupAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["one", "two"]);
        RuleSnapshot snapshot = Snapshot([Rule("one"), Rule("two")]);
        RuleGroupManagementProjection projection = Projection(group, Member("one", Row(snapshot.Rules[0], 0)), Member("two", Row(snapshot.Rules[1], 1)));
        RuleBatchDeleteResponse response = new(
            RuleBatchDeleteOutcome.PartiallyCompleted,
            new RuleListResponse(true, [snapshot.Rules[1]], TestFirewallConfiguration.Enabled),
            [new RuleBatchDeleteOperationResponse(0, "one", RuleBatchDeleteOperationOutcome.Deleted, null)],
            [1],
            "drift");
        RuleGroup remaining = group with { RuleIds = ["two"] };
        host.Mutations.Setup(service => service.BatchDeleteRulesAsync(It.IsAny<RuleListResponse>(), It.IsAny<IReadOnlyList<int>>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(response);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([group])
            .ReturnsAsync([remaining]);

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot, "key");

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.BatchIncomplete, result.Outcome);
        Assert.AreSame(response, result.BatchResponse);
        host.Groups.Verify(service => service.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_StaleMembershipRejectsBeforeSigningOrMutationAsync()
    {
        TestHost host = new();
        RuleGroup group = Group(["stale"]);
        RuleSnapshot snapshot = Snapshot([]);
        RuleGroupManagementProjection projection = Projection(group, Member("stale"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.Service.DeleteAsync(projection, snapshot, "key"));

        host.Mutations.VerifyNoOtherCalls();
        host.Groups.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteIfEmptyAsync_ConcurrentMembershipConflictRetainsRefreshedGroupAsync()
    {
        TestHost host = new();
        RuleGroup empty = Group([]);
        RuleGroup reassigned = Group(["new-member"]);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([empty])
            .ReturnsAsync([reassigned]);
        host.Groups.Setup(service => service.DeleteAsync(empty.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiRequestException(HttpStatusCode.Conflict, "in use"));

        RuleGroupCleanupResult result = await host.Service.DeleteIfEmptyAsync(empty.Id);

        Assert.IsFalse(result.Deleted);
        CollectionAssert.AreEqual(new[] { reassigned }, result.Groups.ToArray());
    }

    [TestMethod]
    public async Task DeleteAsync_EmptyGroupUsesRaceSafeEmptyCleanupAsync()
    {
        TestHost host = new();
        RuleGroup empty = Group([]);
        RuleGroup reassigned = Group(["new-member"]);
        RuleGroupManagementProjection projection = Projection(empty);
        host.Groups.SetupSequence(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([empty])
            .ReturnsAsync([reassigned]);
        host.Groups.Setup(service => service.DeleteAsync(empty.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiRequestException(HttpStatusCode.Conflict, "in use"));

        RuleGroupDeletionWorkflowResult result = await host.Service.DeleteAsync(projection, snapshot: null, privateKey: null);

        Assert.AreEqual(RuleGroupDeletionWorkflowOutcome.GroupRetained, result.Outcome);
        host.Mutations.VerifyNoOtherCalls();
    }

    private static RuleGroup Group(IReadOnlyList<string> ruleIds) => new(Guid.Parse("0199aabb-ccdd-7eef-8000-000000000001"), "group", null, ruleIds);

    private static RuleMetadata Metadata(RuleGroup group) => new(Guid.Parse("0199aabb-ccdd-7eef-8000-000000000002"), null, [], new RuleGroupMembership(group.Id, group.Name, group.Comment));

    private static ListedFirewallRule Rule(string ruleId) => new()
    {
        Parsed = true,
        RuleId = ruleId,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
    };

    private static RuleSnapshot Snapshot(IReadOnlyList<ListedFirewallRule> rules, IReadOnlyDictionary<string, RuleMetadata>? metadata = null) =>
        new(true, rules, TestFirewallConfiguration.Enabled, metadata ?? new Dictionary<string, RuleMetadata>(StringComparer.Ordinal));

    private static RuleRowProjection Row(ListedFirewallRule rule, int occurrenceId) =>
        new(rule, FirewallAddressFamily.IPv4, occurrenceId, occurrenceId + 1, FamilyCount: 1, CanOrder: true, CanMutate: true, PositionChange: null);

    private static RuleGroupMemberProjection Member(string ruleId, params RuleRowProjection[] occurrences) => new(ruleId, occurrences);

    private static RuleGroupManagementProjection Projection(RuleGroup group, params RuleGroupMemberProjection[] members) =>
        new(group, members, MemberResolutionAvailable: true);

    private sealed class TestHost
    {
        public Mock<IRuleMutationService> Mutations { get; } = new(MockBehavior.Strict);

        public Mock<IRuleGroupCatalogService> Groups { get; } = new(MockBehavior.Strict);

        public RuleGroupDeletionWorkflowService Service => new(Mutations.Object, Groups.Object);
    }
}
