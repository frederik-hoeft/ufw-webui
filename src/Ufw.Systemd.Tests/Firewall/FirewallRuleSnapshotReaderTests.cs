using Moq;
using System.Collections.Immutable;
using Ufw.Shared.Firewall;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleSnapshotReaderTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReadAsync_ParsesSuccessfulProcessOutputAndReadsDefaultsAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner
            .Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUfwCommand command, CancellationToken _) => ProcessResult(command is UfwVerboseStatusCommand
                ? UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES)
                : UfwStatusFixtures.TWO_RULES));
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        defaultsReader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestFirewallConfiguration.Enabled);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        FirewallRuleSnapshotReadResult.Success success = Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Success>(result);
        Assert.IsTrue(success.Snapshot.Active);
        Assert.HasCount(2, success.Snapshot.Rules);
        Assert.AreEqual(TestFirewallConfiguration.Enabled, success.Snapshot.Configuration);
        runner.Verify(value => value.ExecuteAsync(It.Is<IUfwCommand>(command => command is UfwListCommand), It.IsAny<CancellationToken>()), Times.Once);
        runner.Verify(value => value.ExecuteAsync(It.Is<IUfwCommand>(command => command is UfwVerboseStatusCommand), It.IsAny<CancellationToken>()), Times.Once);
        defaultsReader.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ReadAsync_ActiveStatusUsesEffectivePoliciesNotConfiguredPoliciesAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUfwCommand command, CancellationToken _) => ProcessResult(command is UfwVerboseStatusCommand
                ? UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES)
                    .Replace("deny (incoming), allow (outgoing), deny (routed)", "reject (incoming), deny (outgoing), disabled (routed)", StringComparison.Ordinal)
                : UfwStatusFixtures.TWO_RULES));
        FirewallConfigurationSnapshot configured = new(false, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Allow);
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        defaultsReader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configured);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        FirewallSnapshot snapshot = Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Success>(result).Snapshot;
        Assert.IsFalse(snapshot.Configuration.IPv6Enabled);
        Assert.AreEqual(FirewallDefaultPolicy.Reject, snapshot.Configuration.IncomingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Deny, snapshot.Configuration.OutgoingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Disabled, snapshot.Configuration.RoutedPolicy);
    }

    [TestMethod]
    public async Task ReadAsync_InactiveStatusRetainsConfiguredPolicyFallbackAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUfwCommand command, CancellationToken _) => ProcessResult("Status: inactive"));
        FirewallConfigurationSnapshot configured = new(false, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Reject, FirewallDefaultPolicy.Deny);
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        defaultsReader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configured);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        FirewallSnapshot snapshot = Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Success>(result).Snapshot;
        Assert.IsFalse(snapshot.Active);
        Assert.AreEqual(configured, snapshot.Configuration);
    }

    [TestMethod]
    public async Task ReadAsync_DivergentVerboseRules_FailsClosedBeforeReadingDefaultsAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner.Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUfwCommand command, CancellationToken _) => ProcessResult(command is UfwVerboseStatusCommand
                ? UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES).Replace("22/tcp", "23/tcp", StringComparison.Ordinal)
                : UfwStatusFixtures.TWO_RULES));
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Failure>(result);
        defaultsReader.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReadAsync_CallerCancellationAfterProcessCompletion_StopsBeforeParsingDefaultsAsync()
    {
        using CancellationTokenSource cancellationSource = new();
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner
            .Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await cancellationSource.CancelAsync();
                return ProcessResult(UfwStatusFixtures.EMPTY_ACTIVE);
            });
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => reader.ReadAsync(cancellationSource.Token));

        defaultsReader.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ReadAsync_UnparseableSuccessfulOutput_FailsBeforeReadingDefaultsAsync()
    {
        Mock<IUfwRunner> runner = new(MockBehavior.Strict);
        runner
            .Setup(value => value.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessResult("ufw emitted an unexpected response\n"));
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        FirewallRuleSnapshotReadResult.Failure failure = Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Failure>(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(failure.Diagnostic));
        defaultsReader.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UfwProcessResult ProcessResult(string standardOutput) =>
        new(0, standardOutput, string.Empty, ["status", "numbered"], CancellationRequested: false);
}
