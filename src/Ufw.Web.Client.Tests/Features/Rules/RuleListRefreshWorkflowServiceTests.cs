using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleListRefreshWorkflowServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_LoadsAuthorityThenVisibleHostsAsync()
    {
        using CancellationTokenSource lifetime = new();
        TestHost host = new();
        List<string> order = [];
        RuleSnapshot snapshot = Snapshot();
        KnownHostInventoryItem visible = Host("visible", true);
        KnownHostInventoryItem hidden = Host("hidden", false);
        host.Inventory.Setup(service => service.GetAsync(lifetime.Token)).Callback(() => order.Add("rules")).ReturnsAsync(snapshot);
        host.Hosts.Setup(service => service.RefreshAsync(lifetime.Token)).Callback(() => order.Add("hosts"))
            .ReturnsAsync(new KnownHostInventoryResponse([hidden, visible]));

        RuleListRefreshResult result = await host.Service.RefreshAsync(RuleInventoryState.Initial.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual)), [], lifetime.Token);

        Assert.IsTrue(result.State.IsCurrent);
        Assert.AreSame(snapshot, result.State.Snapshot);
        Assert.HasCount(1, result.KnownHosts);
        Assert.AreSame(visible, result.KnownHosts[0]);
        CollectionAssert.AreEqual(new[] { "rules", "hosts" }, order);
    }

    [TestMethod]
    public async Task RefreshAsync_FailedRulesPreservePreviousSnapshotAndHostsAsync()
    {
        TestHost host = new();
        RuleSnapshot snapshot = Snapshot();
        RuleInventoryState current = Loaded(snapshot);
        KnownHostInventoryItem previous = Host("previous", true);
        HttpRequestException failure = new("offline");
        host.Inventory.Setup(service => service.GetAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(true);
        ClientError error = new(ClientErrorKind.Unavailable, "offline", true);
        host.Errors.Setup(mapper => mapper.Describe(failure)).Returns(error);

        RuleListRefreshResult result = await host.Service.RefreshAsync(current.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.AfterMutation)), [previous]);

        Assert.IsTrue(result.State.IsStale);
        Assert.AreSame(snapshot, result.State.Snapshot);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationCommitted, result.State.StaleReason);
        Assert.AreSame(error, result.State.Error);
        Assert.AreSame(previous, result.KnownHosts[0]);
        host.Hosts.Verify(service => service.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RefreshAsync_KnownHostFailureRetainsNewAuthorityAndUsesCachedVisibleHostsAsync()
    {
        TestHost host = new();
        RuleSnapshot snapshot = Snapshot();
        KnownHostInventoryItem cached = Host("cached", true);
        host.Inventory.Setup(service => service.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(snapshot);
        HttpRequestException failure = new("known hosts unavailable");
        host.Hosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Hosts.SetupGet(service => service.Current).Returns(new KnownHostInventoryResponse([Host("hidden", false), cached]));
        host.Errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(true);

        RuleListRefreshResult result = await host.Service.RefreshAsync(RuleInventoryState.Initial.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual)), []);

        Assert.IsTrue(result.State.IsCurrent);
        Assert.AreSame(snapshot, result.State.Snapshot);
        Assert.HasCount(1, result.KnownHosts);
        Assert.AreSame(cached, result.KnownHosts[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_CanceledHostLookupPropagatesWithoutReportingFailedFirewallRefreshAsync()
    {
        using CancellationTokenSource lifetime = new();
        TestHost host = new();
        host.Inventory.Setup(service => service.GetAsync(lifetime.Token)).ReturnsAsync(Snapshot());
        host.Hosts.Setup(service => service.RefreshAsync(lifetime.Token)).Callback(lifetime.Cancel).ThrowsAsync(new OperationCanceledException(lifetime.Token));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => host.Service.RefreshAsync(
            RuleInventoryState.Initial.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual)), [], lifetime.Token));
        host.Errors.VerifyNoOtherCalls();
    }

    private static RuleSnapshot Snapshot() => new(true, [], TestFirewallConfiguration.Enabled);

    private static RuleInventoryState Loaded(RuleSnapshot snapshot) => RuleInventoryState.Initial
        .MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual))
        .MoveNext(new RuleInventoryTransition.RefreshCompleted(snapshot));

    private static KnownHostInventoryItem Host(string name, bool visible) => new(Guid.CreateVersion7(), name, "192.0.2.1", FirewallAddressFamily.IPv4, null, visible);

    private sealed class TestHost
    {
        public Mock<IRuleInventoryService> Inventory { get; } = new(MockBehavior.Strict);
        public Mock<IKnownHostInventoryService> Hosts { get; } = new(MockBehavior.Strict);
        public Mock<IClientErrorMapper> Errors { get; } = new(MockBehavior.Strict);
        public RuleListRefreshWorkflowService Service => new(Inventory.Object, Hosts.Object, Errors.Object);
    }
}
