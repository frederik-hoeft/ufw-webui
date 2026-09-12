using Ufw.Shared.Firewall;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FileReorderRecoveryJournalTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task WriteAsync_PersistsEntryAcrossJournalInstancesAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal writer = new(configuration);
            ReorderRecoveryJournalEntry expected = CreateEntry();

            await writer.WriteAsync(expected, TestContext.CancellationToken);

            FileReorderRecoveryJournal reader = new(configuration);
            ReorderRecoveryJournalEntry? actual = await reader.ReadAsync(TestContext.CancellationToken);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.FormatVersion, actual.FormatVersion);
            Assert.AreEqual(expected.OriginalDisplayNumber, actual.OriginalDisplayNumber);
            Assert.AreEqual(expected.ExpectedMultiplicity, actual.ExpectedMultiplicity);
            AssertRuleEqual(expected.Rule, actual.Rule);
            Assert.IsNotNull(actual.PreviousAnchor?.Rule);
            AssertRuleEqual(expected.PreviousAnchor!.Rule!, actual.PreviousAnchor!.Rule!);
            Assert.AreEqual(expected.NextAnchor?.RawLine, actual.NextAnchor?.RawLine);
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_InvalidRecoveryState_FailsClosedWithoutDeletingJournalAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal journal = new(configuration);
            await journal.WriteAsync(CreateEntry(), TestContext.CancellationToken);
            string persisted = await File.ReadAllTextAsync(path, TestContext.CancellationToken);
            persisted = persisted.Replace("\"expectedMultiplicity\": 1", "\"expectedMultiplicity\": 0", StringComparison.Ordinal);
            await File.WriteAllTextAsync(path, persisted, TestContext.CancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                _ = await journal.ReadAsync(TestContext.CancellationToken));

            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadAsync_UnknownFormatVersion_FailsClosedWithoutDeletingJournalAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal journal = new(configuration);
            await journal.WriteAsync(CreateEntry(), TestContext.CancellationToken);
            string persisted = await File.ReadAllTextAsync(path, TestContext.CancellationToken);
            persisted = persisted.Replace("\"formatVersion\": 1", "\"formatVersion\": 99", StringComparison.Ordinal);
            await File.WriteAllTextAsync(path, persisted, TestContext.CancellationToken);

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
                _ = await journal.ReadAsync(TestContext.CancellationToken));

            StringAssert.Contains(exception.Message, "format version");
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ClearAsync_RemovesPersistedAndTemporaryJournalFilesAsync()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "reorder-recovery.json");
        try
        {
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: path));
            FileReorderRecoveryJournal journal = new(configuration);
            await journal.WriteAsync(CreateEntry(), TestContext.CancellationToken);
            await File.WriteAllTextAsync(path + ".tmp", "stale", TestContext.CancellationToken);

            await journal.ClearAsync(TestContext.CancellationToken);

            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".tmp"));
            Assert.IsNull(await journal.ReadAsync(TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReorderRecoveryJournalEntry CreateEntry() => new(
        ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
        CreateRule("22"),
        2,
        1,
        new RuleRecoveryAnchor(CreateRule("80"), null),
        new RuleRecoveryAnchor(null, "[ 3] opaque rule"));

    private static FirewallRuleSpecification CreateRule(string port) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "0.0.0.0/0",
        Destination = "0.0.0.0/0",
        DestinationPorts = port,
    };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-reorder-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void AssertRuleEqual(FirewallRuleSpecification expected, FirewallRuleSpecification actual)
    {
        Assert.AreEqual(expected.Action, actual.Action);
        Assert.AreEqual(expected.AddressFamily, actual.AddressFamily);
        Assert.AreEqual(expected.Direction, actual.Direction);
        Assert.AreEqual(expected.Protocol, actual.Protocol);
        Assert.AreEqual(expected.Source, actual.Source);
        Assert.AreEqual(expected.SourcePorts, actual.SourcePorts);
        Assert.AreEqual(expected.SourceInterface, actual.SourceInterface);
        Assert.AreEqual(expected.Destination, actual.Destination);
        Assert.AreEqual(expected.DestinationPorts, actual.DestinationPorts);
        Assert.AreEqual(expected.DestinationInterface, actual.DestinationInterface);
        Assert.AreEqual(expected.Comment, actual.Comment);
    }
}
