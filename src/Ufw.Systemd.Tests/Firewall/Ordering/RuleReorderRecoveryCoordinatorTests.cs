using Moq;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class RuleReorderRecoveryCoordinatorTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EnsurePresentAsync_UniqueNextAnchorTakesPrecedenceOverPreviousAnchorAsync()
    {
        RuleListResponse snapshot = ToResponse(Snapshot("80", "8080", "443"));
        RuleListResponse confirmed = ToResponse(Snapshot("80", "8080", "22", "443"));
        ReorderRecoveryJournalEntry entry = Entry(
            Rule(FirewallAddressFamily.IPv4, "22"),
            originalFamilyPosition: 2,
            previous: Rule(FirewallAddressFamily.IPv4, "80"),
            next: Rule(FirewallAddressFamily.IPv4, "443"));

        string[] command = await ExecuteRecoveryAsync(entry, snapshot, confirmed);

        CollectionAssert.AreEqual(
            new[] { "insert", "3", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            command);
    }

    [TestMethod]
    public async Task EnsurePresentAsync_AmbiguousNextAnchorFallsBackToUniquePreviousAnchorAsync()
    {
        RuleListResponse snapshot = ToResponse(Snapshot("80", "443", "443"));
        RuleListResponse confirmed = ToResponse(Snapshot("80", "22", "443", "443"));
        ReorderRecoveryJournalEntry entry = Entry(
            Rule(FirewallAddressFamily.IPv4, "22"),
            originalFamilyPosition: 3,
            previous: Rule(FirewallAddressFamily.IPv4, "80"),
            next: Rule(FirewallAddressFamily.IPv4, "443"));

        string[] command = await ExecuteRecoveryAsync(entry, snapshot, confirmed);

        CollectionAssert.AreEqual(
            new[] { "insert", "2", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            command);
    }

    [TestMethod]
    public async Task EnsurePresentAsync_CrossFamilyOrOpaqueNextAnchorIsNotTrustedAndHistoricalPositionCanAppendAsync()
    {
        RuleListResponse crossFamilySnapshot = ToResponse(SnapshotTokens("80", "443v6"));
        RuleListResponse crossFamilyConfirmed = ToResponse(SnapshotTokens("80", "22", "443v6"));
        ReorderRecoveryJournalEntry crossFamilyEntry = Entry(
            Rule(FirewallAddressFamily.IPv4, "22"),
            originalFamilyPosition: 2,
            previous: null,
            next: Rule(FirewallAddressFamily.IPv6, "443"));

        string[] crossFamilyCommand = await ExecuteRecoveryAsync(crossFamilyEntry, crossFamilySnapshot, crossFamilyConfirmed);

        CollectionAssert.AreEqual(
            new[] { "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            crossFamilyCommand);

        ListedFirewallRule opaque = new() { Parsed = false, RawLine = "[ 2] unsupported ipv4 syntax" };
        RuleListResponse opaqueSnapshot = new(crossFamilySnapshot.Active, [crossFamilySnapshot.Rules[0], opaque], crossFamilySnapshot.Configuration);
        RuleListResponse opaqueConfirmed = ToResponse(Snapshot("22", "80"));
        ReorderRecoveryJournalEntry opaqueEntry = new(
            ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
            Rule(FirewallAddressFamily.IPv4, "22"),
            1,
            1,
            null,
            new RuleRecoveryAnchor(null, opaque.RawLine));

        string[] opaqueCommand = await ExecuteRecoveryAsync(opaqueEntry, opaqueSnapshot, opaqueConfirmed);

        CollectionAssert.AreEqual(
            new[] { "insert", "1", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            opaqueCommand);
    }

    private async Task<string[]> ExecuteRecoveryAsync(ReorderRecoveryJournalEntry entry, RuleListResponse snapshot, RuleListResponse confirmed)
    {
        Mock<IFirewallRuleSnapshotReader> snapshotReader = new(MockBehavior.Strict);
        snapshotReader.Setup(reader => reader.ReadAsync(CancellationToken.None)).ReturnsAsync(new FirewallRuleSnapshotReadResult.Success(confirmed));
        string[]? arguments = null;
        Mock<IUfwProcessExecutor> processExecutor = new(MockBehavior.Strict);
        processExecutor
            .Setup(executor => executor.ExecuteAsync(It.IsAny<IUfwCommand>(), "recovering an interrupted reorder move", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUfwCommand command, string _, CancellationToken _) =>
            {
                arguments = command.BuildArguments().ToArray();
                return new UfwProcessExecutionResult(true, false, null, false);
            });
        Mock<IReorderRecoveryJournal> journal = new(MockBehavior.Strict);
        journal.Setup(candidate => candidate.ClearAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        RuleReorderRecoveryCoordinator coordinator = new(snapshotReader.Object, processExecutor.Object, new UfwRuleCommandRenderer(), journal.Object, new ConsoleLogger());

        RuleRecoveryResult result = await coordinator.EnsurePresentAsync(entry, snapshot, TestContext.CancellationToken);

        Assert.IsTrue(result.PresenceConfirmed);
        Assert.IsTrue(result.InsertionAttempted);
        Assert.IsNotNull(arguments);
        journal.Verify(candidate => candidate.ClearAsync(CancellationToken.None), Times.Once);
        return arguments;
    }

    private static ReorderRecoveryJournalEntry Entry(
        FirewallRuleSpecification rule,
        int originalFamilyPosition,
        FirewallRuleSpecification? previous,
        FirewallRuleSpecification? next) =>
        new(
            ReorderRecoveryJournalEntry.CURRENT_FORMAT_VERSION,
            rule,
            originalFamilyPosition,
            1,
            previous is null ? null : new RuleRecoveryAnchor(previous, null),
            next is null ? null : new RuleRecoveryAnchor(next, null));

    private static UfwStatusSnapshot Snapshot(params string[] ports)
    {
        string[] rows = ports.Select(static (port, index) => $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere").ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static UfwStatusSnapshot SnapshotTokens(params string[] tokens)
    {
        string[] rows = tokens.Select(static (token, index) =>
        {
            bool ipv6 = token.EndsWith("v6", StringComparison.Ordinal);
            string port = ipv6 ? token[..^2] : token;
            return ipv6
                ? $"[ {index + 1}] {port}/tcp (v6)                ALLOW IN    Anywhere (v6)"
                : $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere";
        }).ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot, TestFirewallConfiguration.Enabled);

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string port) => new()
    {
        AddressFamily = family,
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Source = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        Destination = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        DestinationPorts = port,
        Protocol = FirewallProtocol.Tcp,
    };
}
