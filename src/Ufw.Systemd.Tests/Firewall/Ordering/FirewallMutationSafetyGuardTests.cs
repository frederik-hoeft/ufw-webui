using Moq;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FirewallMutationSafetyGuardTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EnsureSafeAsync_NoOutstandingJournal_DoesNothingAsync()
    {
        Mock<IReorderRecoveryJournal> journal = new(MockBehavior.Strict);
        journal
            .Setup(candidate => candidate.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReorderRecoveryJournalEntry?)null);
        Mock<IRuleReorderRecoveryCoordinator> recovery = new(MockBehavior.Strict);
        FirewallMutationSafetyGuard guard = new(journal.Object, recovery.Object);

        await guard.EnsureSafeAsync(TestContext.CancellationToken);

        recovery.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task EnsureSafeAsync_OutstandingJournal_RecoversBeforeReturningAsync()
    {
        ReorderRecoveryJournalEntry entry = TestEntry();
        Mock<IReorderRecoveryJournal> journal = new(MockBehavior.Strict);
        journal
            .Setup(candidate => candidate.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        Mock<IRuleReorderRecoveryCoordinator> recovery = new(MockBehavior.Strict);
        recovery
            .Setup(candidate => candidate.EnsurePresentAsync(entry, null, CancellationToken.None))
            .ReturnsAsync(new RuleRecoveryResult(true, false, null, null));
        FirewallMutationSafetyGuard guard = new(journal.Object, recovery.Object);

        await guard.EnsureSafeAsync(TestContext.CancellationToken);

        recovery.VerifyAll();
    }

    [TestMethod]
    public async Task EnsureSafeAsync_UnrecoverableJournal_BlocksMutationAsync()
    {
        ReorderRecoveryJournalEntry entry = TestEntry();
        Mock<IReorderRecoveryJournal> journal = new(MockBehavior.Strict);
        journal
            .Setup(candidate => candidate.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        Mock<IRuleReorderRecoveryCoordinator> recovery = new(MockBehavior.Strict);
        recovery
            .Setup(candidate => candidate.EnsurePresentAsync(entry, null, CancellationToken.None))
            .ReturnsAsync(new RuleRecoveryResult(false, false, null, "state unavailable"));
        FirewallMutationSafetyGuard guard = new(journal.Object, recovery.Object);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await guard.EnsureSafeAsync(TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "mutations remain blocked");
    }

    private static ReorderRecoveryJournalEntry TestEntry() => new(
        ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
        new Ufw.Shared.Firewall.FirewallRuleSpecification
        {
            Action = Ufw.Shared.Firewall.FirewallAction.Allow,
            AddressFamily = Ufw.Shared.Firewall.FirewallAddressFamily.IPv4,
            Direction = Ufw.Shared.Firewall.FirewallDirection.In,
            Protocol = Ufw.Shared.Firewall.FirewallProtocol.Tcp,
            Source = "0.0.0.0/0",
            Destination = "0.0.0.0/0",
            DestinationPorts = "22",
        },
        1,
        1,
        null,
        null);
}
