using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Client.Features.Rules.Api;
using Ufw.Web.Client.Features.Status.Api;
using Ufw.Web.Client.Features.Status;
using Ufw.Web.Client.Infrastructure.Intent;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Status;

[TestClass]
public sealed class OperationalStatusServiceTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 17, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task RefreshAsync_SuccessPublishesIndependentOperationalSnapshotAndChangeNotificationsAsync()
    {
        Mock<IManagementApiHealthClient> management = SuccessfulManagementProbe();
        Mock<IDaemonStatusApiClient> daemon = SuccessfulDaemonProbe();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        MutableTimeProvider clock = new(s_now);
        TaskCompletionSource<RuleInventoryResponse> rulesResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rules.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>())).Returns(rulesResult.Task);
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, clock);
        List<bool> refreshingStates = [];
        service.Changed += () => refreshingStates.Add(service.IsRefreshing);

        Task refresh = service.RefreshAsync();
        Assert.IsTrue(service.IsRefreshing);
        clock.Advance(TimeSpan.FromMilliseconds(125));
        rulesResult.SetResult(Inventory(new RuleListResponse(true, [new ListedFirewallRule(), new ListedFirewallRule()], TestFirewallConfiguration.Enabled)));
        await refresh;

        Assert.IsFalse(service.IsRefreshing);
        CollectionAssert.AreEqual(new[] { true, false }, refreshingStates);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.ManagementApi);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.Daemon);
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
    public async Task RefreshAsync_RuleReadCannotRescueFailedDaemonLivenessProbeAsync()
    {
        Mock<IManagementApiHealthClient> management = SuccessfulManagementProbe();
        Mock<IDaemonStatusApiClient> daemon = new();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        HttpRequestException daemonFailure = new("daemon unavailable");
        ClientError mapped = new(ClientErrorKind.Unavailable, "unavailable", true);
        daemon.Setup(client => client.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(daemonFailure));
        rules.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Inventory(new RuleListResponse(true, [], TestFirewallConfiguration.Enabled)));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        errors.Setup(mapper => mapper.Describe(daemonFailure)).Returns(mapped);
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Available, service.Current.ManagementApi);
        Assert.AreEqual(OperationalAvailability.Unavailable, service.Current.Daemon);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.FirewallSnapshot);
        Assert.AreSame(mapped, service.Current.DaemonError);
        Assert.IsNull(service.Current.FirewallError);
    }

    [TestMethod]
    public async Task RefreshAsync_UpstreamFailureLeavesFailedDownstreamProbesUnknownAsync()
    {
        Mock<IManagementApiHealthClient> management = new();
        Mock<IDaemonStatusApiClient> daemon = new();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        HttpRequestException managementFailure = new("management");
        HttpRequestException daemonFailure = new("daemon");
        HttpRequestException rulesFailure = new("rules");
        HttpRequestException intentFailure = new("intent");
        management.Setup(client => client.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(managementFailure));
        daemon.Setup(client => client.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(daemonFailure));
        rules.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException<RuleInventoryResponse>(rulesFailure));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException<IntentContextResponse>(intentFailure));
        errors.Setup(mapper => mapper.Describe(It.IsAny<Exception>()))
            .Returns((Exception exception) => new ClientError(ClientErrorKind.Unavailable, exception.Message, true));
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Unavailable, service.Current.ManagementApi);
        Assert.AreEqual(OperationalAvailability.Unknown, service.Current.Daemon);
        Assert.AreEqual(OperationalAvailability.Unknown, service.Current.FirewallSnapshot);
        Assert.IsNotNull(service.Current.ManagementApiError);
        Assert.IsNotNull(service.Current.DaemonError);
        Assert.IsNotNull(service.Current.FirewallError);
    }

    [TestMethod]
    public async Task RefreshAsync_UpstreamFailureDoesNotOverwriteSuccessfulDownstreamProbesAsync()
    {
        Mock<IManagementApiHealthClient> management = new();
        Mock<IDaemonStatusApiClient> daemon = SuccessfulDaemonProbe();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        HttpRequestException managementFailure = new("management");
        management.Setup(client => client.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(managementFailure));
        rules.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Inventory(new RuleListResponse(false, [], TestFirewallConfiguration.Enabled)));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        errors.Setup(mapper => mapper.Describe(managementFailure))
            .Returns(new ClientError(ClientErrorKind.Unavailable, "management", true));
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Unavailable, service.Current.ManagementApi);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.Daemon);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.FirewallSnapshot);
        Assert.AreEqual(false, service.Current.FirewallActive);
    }

    [TestMethod]
    public async Task RefreshAsync_IntentContextFailureDoesNotChangeDaemonLivenessAsync()
    {
        Mock<IManagementApiHealthClient> management = SuccessfulManagementProbe();
        Mock<IDaemonStatusApiClient> daemon = SuccessfulDaemonProbe();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        InvalidOperationException failure = new("intent metadata unavailable");
        ClientError mapped = new(ClientErrorKind.Unexpected, failure.Message, true);
        rules.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Inventory(new RuleListResponse(true, [], TestFirewallConfiguration.Enabled)));
        intent.Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromException<IntentContextResponse>(failure));
        errors.Setup(mapper => mapper.Describe(failure)).Returns(mapped);
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, new MutableTimeProvider(s_now));

        await service.RefreshAsync();

        Assert.AreEqual(OperationalAvailability.Available, service.Current.Daemon);
        Assert.AreEqual(OperationalAvailability.Available, service.Current.FirewallSnapshot);
        Assert.IsNull(service.Current.IntentProtocolVersion);
        Assert.IsNull(service.Current.DeploymentId);
        Assert.AreSame(mapped, service.Current.IntentContextError);
    }

    [TestMethod]
    public async Task RefreshAsync_CallerCancellationPropagatesAndAlwaysResetsRefreshingStateAsync()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Mock<IManagementApiHealthClient> management = new();
        Mock<IDaemonStatusApiClient> daemon = new();
        Mock<IRuleApiClient> rules = new();
        Mock<IIntentContextApiClient> intent = new();
        Mock<IClientErrorMapper> errors = new();
        using OperationalStatusService service = CreateService(management, daemon, rules, intent, errors, TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.RefreshAsync(cancellation.Token));

        Assert.IsFalse(service.IsRefreshing);
    }

    private static RuleInventoryResponse Inventory(RuleListResponse firewall) => new()
    {
        Firewall = firewall,
    };

    private static OperationalStatusService CreateService(
        Mock<IManagementApiHealthClient> management,
        Mock<IDaemonStatusApiClient> daemon,
        Mock<IRuleApiClient> rules,
        Mock<IIntentContextApiClient> intent,
        Mock<IClientErrorMapper> errors,
        TimeProvider timeProvider) =>
        new(management.Object, daemon.Object, rules.Object, intent.Object, errors.Object, timeProvider);

    private static Mock<IManagementApiHealthClient> SuccessfulManagementProbe()
    {
        Mock<IManagementApiHealthClient> client = new();
        client.Setup(probe => probe.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return client;
    }

    private static Mock<IDaemonStatusApiClient> SuccessfulDaemonProbe()
    {
        Mock<IDaemonStatusApiClient> client = new();
        client.Setup(probe => probe.ProbeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return client;
    }
}
