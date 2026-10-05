using Moq;
using System.Collections.Immutable;
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
            .ReturnsAsync(ProcessResult(UfwStatusFixtures.TWO_RULES));
        Mock<IUfwDefaultsReader> defaultsReader = new(MockBehavior.Strict);
        defaultsReader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestFirewallConfiguration.Enabled);
        FirewallRuleSnapshotReader reader = new(runner.Object, defaultsReader.Object, new ConsoleLogger());

        FirewallRuleSnapshotReadResult result = await reader.ReadAsync(TestContext.CancellationToken);

        FirewallRuleSnapshotReadResult.Success success = Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Success>(result);
        Assert.IsTrue(success.Snapshot.Active);
        Assert.HasCount(2, success.Snapshot.Rules);
        Assert.AreSame(TestFirewallConfiguration.Enabled, success.Snapshot.Configuration);
        runner.Verify(value => value.ExecuteAsync(It.Is<IUfwCommand>(command => command is UfwListCommand), It.IsAny<CancellationToken>()), Times.Once);
        defaultsReader.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        Assert.IsNotNull(failure.Error);
        defaultsReader.Verify(value => value.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UfwProcessResult ProcessResult(string standardOutput) =>
        new(0, standardOutput, string.Empty, ImmutableArray.Create("status", "numbered"), CancellationRequested: false);
}
