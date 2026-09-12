using Moq;
using System.Collections.Immutable;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FirewallReorderRecoveryServiceTests
{
    private static readonly string[] s_recoveryInsertArguments =
        ["insert", "1", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp"];

    private static readonly string[] s_ipv6RecoveryInsertArguments =
        ["insert", "2", "allow", "in", "from", "::/0", "to", "::/0", "port", "22", "proto", "tcp"];

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RecoverAsync_AfterJournalReopen_ReinsertsMissingRuleAndClearsJournalAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal beforeRestart = new(configuration);
            RuleListResponse original = ToResponse(Snapshot("22", "80"));
            ReorderRecoveryJournalEntry entry = new(ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION, original.Rules[0].Rule!, 1, 1, null, null);
            await beforeRestart.WriteAsync(entry, TestContext.CancellationToken);

            Queue<FirewallRuleSnapshotReadResult> snapshots = new([
                ReadResult(Snapshot("80")),
                ReadResult(Snapshot("22", "80")),
            ]);
            Mock<IFirewallRuleSnapshotReader> snapshotReader = new(MockBehavior.Strict);
            snapshotReader
                .Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => snapshots.Dequeue());
            List<string[]> commands = [];
            Mock<IUfwRunner> runner = new(MockBehavior.Strict);
            runner
                .Setup(candidate => candidate.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IUfwCommand command, CancellationToken _) =>
                {
                    ImmutableArray<string> arguments = command.BuildArguments();
                    commands.Add(arguments.ToArray());
                    return new UfwProcessResult(0, string.Empty, string.Empty, arguments, false);
                });

            FileReorderRecoveryJournal afterRestart = new(configuration);
            RuleReorderRecoveryCoordinator coordinator = new(
                snapshotReader.Object,
                runner.Object,
                new UfwRuleCommandRenderer(),
                afterRestart,
                new ConsoleLogger());
            using UfwExecutionGate gate = new();
            FirewallMutationSafetyGuard guard = new(afterRestart, coordinator);
            FirewallReorderRecoveryService service = new(gate, guard);

            await service.RecoverAsync(TestContext.CancellationToken);

            Assert.HasCount(1, commands);
            CollectionAssert.AreEqual(s_recoveryInsertArguments, commands[0]);
            Assert.IsNull(await afterRestart.ReadAsync(TestContext.CancellationToken));
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecoverAsync_Ipv6FallbackPosition_UsesFamilyLocalNumberingAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal journal = new(configuration);
            RuleListResponse original = ToResponse(SnapshotTokens("80", "443", "80v6", "22v6", "443v6"));
            ReorderRecoveryJournalEntry entry = new(
                ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
                original.Rules[3].Rule!,
                2,
                1,
                null,
                null);
            await journal.WriteAsync(entry, TestContext.CancellationToken);

            Queue<FirewallRuleSnapshotReadResult> snapshots = new([
                ReadResult(SnapshotTokens("80", "443", "80v6", "443v6")),
                ReadResult(SnapshotTokens("80", "443", "80v6", "22v6", "443v6")),
            ]);
            Mock<IFirewallRuleSnapshotReader> snapshotReader = new(MockBehavior.Strict);
            snapshotReader.Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => snapshots.Dequeue());
            List<string[]> commands = [];
            Mock<IUfwRunner> runner = new(MockBehavior.Strict);
            runner.Setup(candidate => candidate.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IUfwCommand command, CancellationToken _) =>
                {
                    ImmutableArray<string> arguments = command.BuildArguments();
                    commands.Add(arguments.ToArray());
                    return new UfwProcessResult(0, string.Empty, string.Empty, arguments, false);
                });
            RuleReorderRecoveryCoordinator coordinator = new(
                snapshotReader.Object,
                runner.Object,
                new UfwRuleCommandRenderer(),
                journal,
                new ConsoleLogger());

            RuleRecoveryResult result = await coordinator.EnsurePresentAsync(entry, null, TestContext.CancellationToken);

            Assert.IsTrue(result.PresenceConfirmed);
            Assert.IsTrue(result.InsertionAttempted);
            CollectionAssert.AreEqual(s_ipv6RecoveryInsertArguments, commands.Single());
            Assert.IsNull(await journal.ReadAsync(TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task RecoverAsync_WhenPresenceCannotBeConfirmed_FailsClosedAndKeepsJournalAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal journal = new(configuration);
            RuleListResponse original = ToResponse(Snapshot("22"));
            ReorderRecoveryJournalEntry entry = new(ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION, original.Rules[0].Rule!, 1, 1, null, null);
            await journal.WriteAsync(entry, TestContext.CancellationToken);
            Mock<IFirewallRuleSnapshotReader> snapshotReader = new(MockBehavior.Strict);
            snapshotReader
                .Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("read failed"), null));
            Mock<IUfwRunner> runner = new(MockBehavior.Strict);
            RuleReorderRecoveryCoordinator coordinator = new(
                snapshotReader.Object,
                runner.Object,
                new UfwRuleCommandRenderer(),
                journal,
                new ConsoleLogger());
            using UfwExecutionGate gate = new();
            FirewallMutationSafetyGuard guard = new(journal, coordinator);
            FirewallReorderRecoveryService service = new(gate, guard);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await service.RecoverAsync(TestContext.CancellationToken));

            StringAssert.Contains(exception.Message, "could not be recovered safely");
            Assert.IsNotNull(await journal.ReadAsync(TestContext.CancellationToken));
            runner.Verify(
                candidate => candidate.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static UfwStatusSnapshot Snapshot(params string[] ports)
    {
        string[] rows = ports
            .Select(static (port, index) => $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere")
            .ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static UfwStatusSnapshot SnapshotTokens(params string[] tokens)
    {
        string[] rows = tokens.Select(static (token, index) =>
        {
            bool v6 = token.EndsWith("v6", StringComparison.Ordinal);
            string port = v6 ? token[..^2] : token;
            return v6
                ? $"[ {index + 1}] {port}/tcp (v6)                ALLOW IN    Anywhere (v6)"
                : $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere";
        }).ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot);

    private static FirewallRuleSnapshotReadResult ReadResult(UfwStatusSnapshot snapshot) => new(null, snapshot);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-reorder-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
