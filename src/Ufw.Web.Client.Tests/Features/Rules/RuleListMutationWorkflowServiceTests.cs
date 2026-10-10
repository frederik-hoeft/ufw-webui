using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleListMutationWorkflowServiceTests
{
    [TestMethod]
    public async Task UpdateMetadataAsync_UpdatesSnapshotOnlyAfterSuccessfulWriteAsync()
    {
        TestHost host = new();
        RuleInventoryState baseline = Loaded();
        RuleMetadataChange change = new("note", [], null);
        RuleMetadataMutationResponse response = new()
        {
            Metadata = new() { RuleId = "rule", Id = Guid.CreateVersion7(), Notes = "note", Tags = [] },
        };
        host.Metadata.Setup(service => service.UpdateAsync("rule", change, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        RuleInventoryState result = await host.Service.UpdateMetadataAsync(baseline, "rule", change);

        Assert.AreEqual("note", result.Snapshot!.Metadata["rule"].Notes);
        Assert.IsEmpty(baseline.Snapshot!.Metadata);
    }

    [TestMethod]
    public async Task DeleteAsync_DeletesFirewallBeforeGroupCleanupAsync()
    {
        TestHost host = new();
        List<string> steps = [];
        ListedFirewallRule rule = Rule();
        RuleGroup group = Group();
        host.Mutations.Setup(service => service.DeleteRuleAsync(rule, "key", It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("delete")).ReturnsAsync(new RuleMutationResponse("delete", rule));
        RuleGroupCleanupResult cleanup = new(true, []);
        host.Groups.Setup(service => service.DeleteIfEmptyAsync(group.Id, It.IsAny<CancellationToken>()))
            .Callback(() => steps.Add("cleanup")).ReturnsAsync(cleanup);

        RuleListDeletionResult result = await host.Service.DeleteAsync(rule, "key", group);

        Assert.AreSame(cleanup, result.GroupCleanup);
        Assert.IsNull(result.GroupCleanupError);
        CollectionAssert.AreEqual(new[] { "delete", "cleanup" }, steps);
    }

    [TestMethod]
    public async Task DeleteAsync_NoGroupSkipsCleanupAsync()
    {
        TestHost host = new();
        ListedFirewallRule rule = Rule();
        host.Mutations.Setup(service => service.DeleteRuleAsync(rule, "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMutationResponse("delete", rule));

        RuleListDeletionResult result = await host.Service.DeleteAsync(rule, "key", null);

        Assert.IsNull(result.GroupCleanup);
        Assert.IsNull(result.GroupCleanupError);
        host.Groups.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteAsync_GroupCleanupFailureDoesNotDiscardConfirmedFirewallDeleteAsync()
    {
        TestHost host = new();
        ListedFirewallRule rule = Rule();
        RuleGroup group = Group();
        host.Mutations.Setup(service => service.DeleteRuleAsync(rule, "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMutationResponse("delete", rule));
        HttpRequestException failure = new("unavailable");
        host.Groups.Setup(service => service.DeleteIfEmptyAsync(group.Id, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(true);
        ClientError error = new(ClientErrorKind.Unavailable, "group cleanup failed", true);
        host.Errors.Setup(mapper => mapper.Describe(failure)).Returns(error);

        RuleListDeletionResult result = await host.Service.DeleteAsync(rule, "key", group);

        Assert.IsNull(result.GroupCleanup);
        Assert.AreSame(error, result.GroupCleanupError);
        host.Mutations.Verify(service => service.DeleteRuleAsync(rule, "key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_FirewallFailureNeverAttemptsGroupCleanupAsync()
    {
        TestHost host = new();
        ListedFirewallRule rule = Rule();
        HttpRequestException failure = new("delete failed");
        host.Mutations.Setup(service => service.DeleteRuleAsync(rule, "key", It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => host.Service.DeleteAsync(rule, "key", Group()));

        host.Groups.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ApplyOrderingAsync_UsesSnapshotLocalOrderAndAppliesAuthoritativeResultAsync()
    {
        TestHost host = new();
        RuleInventoryState baseline = Loaded();
        RuleOrderingPreview preview = new([0], new HashSet<int>());
        RuleListResponse final = new(true, [Rule()], TestFirewallConfiguration.Enabled);
        RuleReorderResponse response = new(RuleReorderOutcome.Completed, final, [], [], [], Diagnostic: null);
        host.Ordering.Setup(service => service.ApplyAsync(It.Is<RuleListResponse>(value => value.Rules.Count == 1),
            It.Is<IReadOnlyList<int>>(order => order.Count == 1 && order[0] == 0), "key", It.IsAny<CancellationToken>())).ReturnsAsync(response);

        RuleListOrderingResult result = await host.Service.ApplyOrderingAsync(baseline, preview, "key");

        Assert.AreSame(response, result.Response);
        Assert.IsTrue(result.State.IsCurrent);
        Assert.IsNotNull(result.State.Snapshot);
        Assert.HasCount(1, result.BaselineRules);
        CollectionAssert.AreEqual(new[] { 0 }, result.DesiredOrder.ToArray());
    }

    [TestMethod]
    public async Task ApplyOrderingAsync_MissingFinalSnapshotPreservesStaleBaselineAsync()
    {
        TestHost host = new();
        RuleInventoryState baseline = Loaded();
        host.Ordering.Setup(service => service.ApplyAsync(It.IsAny<RuleListResponse>(), It.IsAny<IReadOnlyList<int>>(), "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleReorderResponse(RuleReorderOutcome.StateUncertain, null, [], [], [], Diagnostic: "uncertain"));

        RuleListOrderingResult result = await host.Service.ApplyOrderingAsync(baseline, new RuleOrderingPreview([0], new HashSet<int>()), "key");

        Assert.IsTrue(result.State.IsStale);
        Assert.AreSame(baseline.Snapshot, result.State.Snapshot);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, result.State.StaleReason);
    }

    private static ListedFirewallRule Rule() => new() { RuleId = "rule", Parsed = true, Rule = new() { Action = FirewallAction.Allow, AddressFamily = FirewallAddressFamily.IPv4 } };

    private static RuleGroup Group() => new(Guid.CreateVersion7(), "group", null, ["rule"]);

    private static RuleInventoryState Loaded() => RuleInventoryState.Initial
        .MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual))
        .MoveNext(new RuleInventoryTransition.RefreshCompleted(new RuleSnapshot(true, [Rule()], TestFirewallConfiguration.Enabled)));

    private sealed class TestHost
    {
        public Mock<IRuleMetadataMutationService> Metadata { get; } = new(MockBehavior.Strict);
        public Mock<IRuleMutationService> Mutations { get; } = new(MockBehavior.Strict);
        public Mock<IRuleGroupDeletionWorkflowService> Groups { get; } = new(MockBehavior.Strict);
        public Mock<IRuleOrderingService> Ordering { get; } = new(MockBehavior.Strict);
        public Mock<IClientErrorMapper> Errors { get; } = new(MockBehavior.Strict);
        public RuleListMutationWorkflowService Service => new(Metadata.Object, Mutations.Object, Groups.Object, Ordering.Object, Errors.Object, TimeProvider.System);
    }
}
