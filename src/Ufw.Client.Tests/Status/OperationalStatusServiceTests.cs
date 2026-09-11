using Moq;
using Ufw.Client.Api;
using Ufw.Client.Errors;
using Ufw.Client.Status;
using Ufw.Client.Tests.Support;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.Status;

[TestClass]
public sealed class OperationalStatusServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 17, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RefreshAsync_SuccessPublishesCombinedOperationalSnapshotAndChangeNotificationsAsync()
    {
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        MutableTimeProvider clock = new(s_now);
        TaskCompletionSource<RuleListResponse> rulesResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rules.Setup(client => client.GetRulesAsync(It.IsAny<CancellationToken>())).Returns(rulesResult.Task);
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        using OperationalStatusService service = new(rules.Object, intent.Object, errors.Object, clock);
        List<bool> refreshingStates = [];
        service.Changed += () => refreshingStates.Add(service.IsRefreshing);

        Task refresh = service.RefreshAsync();
        Assert.IsTrue(service.IsRefreshing);
        clock.Advance(TimeSpan.FromMilliseconds(125));
        rulesResult.SetResult(new RuleListResponse(true, [new ListedFirewallRule(), new ListedFirewallRule()]));
        await refresh;

        Assert.IsFalse(service.IsRefreshing);
        CollectionAssert.AreEqual(new[] { true, false }, refreshingStates);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.DaemonBackedApi);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.FirewallSnapshot);
        Assert.AreEqual(true, service.Current.FirewallActive);
        Assert.AreEqual(2, service.Current.RuleCount);
        Assert.AreEqual(IntentProtocol.VERSION, service.Current.IntentProtocolVersion);
        Assert.AreEqual(true, service.Current.IntentProtocolCompatible);
        Assert.AreEqual("deployment", service.Current.DeploymentId);
        Assert.AreEqual(s_now.AddMilliseconds(125), service.Current.CheckedAt);
        Assert.AreEqual(TimeSpan.FromMilliseconds(125), service.Current.RoundTrip);
    }

    [TestMethod]
    public async Task RefreshAsync_PartialFailureKeepsAvailableSubsystemAndMapsOnlyFailedSideAsync()
    {
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        HttpRequestException failure = new("daemon unavailable");
        ClientError mapped = new(ClientErrorKind.Unavailable, "unavailable", true);
        rules.Setup(client => client.GetRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleListResponse(false, []));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<IntentContextResponse>(failure));
        errors.Setup(mapper => mapper.Describe(failure)).Returns(mapped);
        using OperationalStatusService service = new(rules.Object, intent.Object, errors.Object, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Available, service.Current.DaemonBackedApi);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.FirewallSnapshot);
        Assert.AreSame(mapped, service.Current.IntentContextError);
        Assert.IsNull(service.Current.FirewallError);
        Assert.IsNull(service.Current.IntentProtocolVersion);
    }

    [TestMethod]
    public async Task RefreshAsync_TotalFailureMarksBothUnavailableAndMapsBothFailuresAsync()
    {
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        InvalidOperationException intentFailure = new("intent");
        HttpRequestException rulesFailure = new("rules");
        rules.Setup(client => client.GetRulesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<RuleListResponse>(rulesFailure));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<IntentContextResponse>(intentFailure));
        errors.Setup(mapper => mapper.Describe(It.IsAny<Exception>()))
            .Returns((Exception exception) => new ClientError(ClientErrorKind.Unexpected, exception.Message, true));
        using OperationalStatusService service = new(rules.Object, intent.Object, errors.Object, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Unavailable, service.Current.DaemonBackedApi);
        Assert.AreEqual(OperationalAvailability.Unavailable, service.Current.FirewallSnapshot);
        Assert.IsNotNull(service.Current.IntentContextError);
        Assert.IsNotNull(service.Current.FirewallError);
    }

    [TestMethod]
    public async Task RefreshAsync_CallerCancellationPropagatesAndAlwaysResetsRefreshingStateAsync()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        using OperationalStatusService service = new(rules.Object, intent.Object, errors.Object, TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RefreshAsync(cancellation.Token));

        Assert.IsFalse(service.IsRefreshing);
    }
}
