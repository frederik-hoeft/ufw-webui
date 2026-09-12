using Moq;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
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
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class FirewallReorderExecutorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_MinimalMove_DeletesThenInsertsBeforeDesiredAnchorAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "22", "443"));
        RuleReorderExecutionRequest request = harness.Request([1, 0, 2]);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleReorderOperationStatus.Applied, result.Operations[0].Status);
        Assert.IsEmpty(result.BlockedOperations);
        Assert.IsEmpty(result.PendingOperations);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[0]);
        CollectionAssert.AreEqual(
            new[] { "insert", "2", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            harness.Commands[1]);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultipleMoves_ReconcilesFreshStateBeforeEveryDeleteAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443", "8080"),
            Snapshot("80", "443", "8080"),
            Snapshot("80", "443", "8080", "22"),
            Snapshot("80", "443", "8080", "22"),
            Snapshot("443", "8080", "22"),
            Snapshot("443", "8080", "80", "22"),
            Snapshot("443", "8080", "80", "22"),
            Snapshot("8080", "80", "22"),
            Snapshot("8080", "443", "80", "22"));

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([3, 2, 1, 0]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(3, result.Operations);
        Assert.IsTrue(result.Operations.All(static operation => operation.Status == RuleReorderOperationStatus.Applied));
        Assert.HasCount(6, harness.Commands);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[0]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[2]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[4]);
        Assert.IsNotNull(result.FinalSnapshot);
        CollectionAssert.AreEqual(
            new[] { "8080", "443", "80", "22" },
            result.FinalSnapshot.Rules.Select(static rule => rule.Rule!.DestinationPorts).ToArray());
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_StaleBaseline_PerformsNoMutationAsync()
    {
        using ReorderHarness harness = new(Snapshot("22", "80"));
        RuleReorderExecutionRequest request = new(
            FirewallRuleSnapshotFingerprint.Compute(ToResponse(Snapshot("22"))),
            [1, 0]);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReversingNoPortRules_MovesRuleNormallyAsync()
    {
        using ReorderHarness harness = new(
            SnapshotNoPort("ALLOW", "DENY"),
            SnapshotNoPort("DENY"),
            SnapshotNoPort("DENY", "ALLOW"));
        RuleReorderExecutionRequest request = harness.Request([1, 0]);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleReorderOperationStatus.Applied, result.Operations[0].Status);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[0]);
        CollectionAssert.AreEqual(
            new[] { "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0" },
            harness.Commands[1]);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceBeforeFirstMove_ReturnsStaleBaselineWithoutMutationAsync()
    {
        using ReorderHarness harness = new(
            duplicateBaselineBeforeFirstMove: false,
            Snapshot("22", "80", "443"),
            Snapshot("443", "80", "22"));

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(result.Operations);
        Assert.IsEmpty(harness.Commands);
        Assert.HasCount(1, result.BlockedOperations);
        Assert.IsNotNull(result.FinalSnapshot);
        Assert.AreEqual("443", result.FinalSnapshot.Rules[0].Rule?.DestinationPorts);
    }

    [TestMethod]
    public async Task ExecuteAsync_DeleteFailureWithExpectedDeletedState_CompletesPairAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "22", "443"));
        harness.EnqueueProcess(exitCode: 1, standardError: "delete reported failure");
        harness.EnqueueProcess(exitCode: 0);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.AppliedAfterProcessFailure, result.Operations[0].Status);
        Assert.HasCount(2, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_DeleteFailureWithRuleStillPresent_StopsWithoutDuplicateReinsertionAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("22", "80", "443"));
        harness.EnqueueProcess(exitCode: 1, standardError: "delete failed");

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleReorderOperationStatus.PresenceConfirmedAfterInterruption, result.Operations[0].Status);
        Assert.HasCount(1, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
        Assert.IsNotEmpty(result.PendingOperations);
    }

    [TestMethod]
    public async Task ExecuteAsync_InsertFailureWithTargetStateObserved_IsAppliedAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "22", "443"));
        harness.EnqueueProcess(exitCode: 0);
        harness.EnqueueProcess(exitCode: 1, standardError: "insert reported failure");

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.AppliedAfterProcessFailure, result.Operations[0].Status);
        Assert.HasCount(2, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_InsertFailureWithMissingRule_RestoresOriginalNeighborhoodAndReturnsPendingPlanAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "443"),
            Snapshot("22", "80", "443"));
        harness.EnqueueProcess(exitCode: 0);
        harness.EnqueueProcess(exitCode: 1, standardError: "insert rejected");
        harness.EnqueueProcess(exitCode: 0);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.FailedAndRestored, result.Operations[0].Status);
        Assert.HasCount(1, result.BlockedOperations);
        Assert.HasCount(1, result.PendingOperations);
        Assert.AreEqual(0, result.PendingOperations[0].OccurrenceId);
        Assert.HasCount(3, harness.Commands);
        CollectionAssert.AreEqual(
            new[] { "insert", "1", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            harness.Commands[2]);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_AmbiguousDuplicateOccurrences_DoNotProduceUnsafePendingPlanAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "22", "80"),
            Snapshot("22", "22"),
            Snapshot("22", "22"),
            Snapshot("22", "22", "80"));
        harness.EnqueueProcess(exitCode: 0);
        harness.EnqueueProcess(exitCode: 1, standardError: "insert rejected");
        harness.EnqueueProcess(exitCode: 0);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([2, 0, 1]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.FailedAndRestored, result.Operations[0].Status);
        Assert.HasCount(1, result.BlockedOperations);
        Assert.IsEmpty(result.PendingOperations);
        Assert.IsNotNull(result.FinalSnapshot);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostDeleteStructuralDivergence_RestoresRemovedRuleAndStopsPlanAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("443"),
            Snapshot("22", "443"));
        harness.EnqueueProcess(exitCode: 0);
        harness.EnqueueProcess(exitCode: 0);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.FailedAndRestored, result.Operations[0].Status);
        Assert.HasCount(1, result.BlockedOperations);
        Assert.IsEmpty(result.PendingOperations);
        Assert.HasCount(2, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostDeleteReadFailure_FailsClosedAndRetainsRecoveryJournalAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            null,
            null);
        harness.EnqueueProcess(exitCode: 0);

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.RecoveryFailed, result.Outcome);
        Assert.HasCount(1, harness.Commands);
        Assert.AreEqual(RuleReorderOperationStatus.RecoveryFailed, result.Operations[0].Status);
        Assert.IsNotNull(harness.Journal.Entry);
        Assert.IsNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceBeforeLaterMove_BlocksRemainingPlanBeforeDeletingAgainAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443", "8080"),
            Snapshot("80", "443", "8080"),
            Snapshot("80", "443", "8080", "22"),
            Snapshot("80", "8080", "443", "22"));

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([3, 2, 1, 0]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleReorderOperationStatus.Applied, result.Operations[0].Status);
        Assert.HasCount(2, harness.Commands);
        Assert.HasCount(2, result.BlockedOperations);
        Assert.IsNotEmpty(result.PendingOperations);
        Assert.IsNotNull(result.FinalSnapshot);
        Assert.AreEqual("8080", result.FinalSnapshot.Rules[1].Rule?.DestinationPorts);
    }

    [TestMethod]
    public async Task ExecuteAsync_RecoveryFailure_LeavesDurableJournalAndReportsRecoveryFailureAsync()
    {
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "443"),
            Snapshot("80", "443"));
        harness.EnqueueProcess(exitCode: 0);
        harness.EnqueueProcess(exitCode: 1, standardError: "planned insert failed");
        harness.EnqueueProcess(exitCode: 1, standardError: "recovery failed");

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0, 2]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.RecoveryFailed, result.Outcome);
        Assert.AreEqual(RuleReorderOperationStatus.RecoveryFailed, result.Operations[0].Status);
        Assert.IsNotNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_RunnerCancellationWithoutDeleteResult_ReconcilesBeforeClearingJournalAsync()
    {
        using CancellationTokenSource cancellation = new();
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("22", "80", "443"));
        harness.EnqueueProcess(
            exitCode: 0,
            onExecute: cancellation.Cancel,
            exception: new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await harness.Executor.ExecuteAsync(harness.Request([1, 0, 2]), cancellation.Token));

        Assert.HasCount(1, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationDuringDelete_RecoversRuleBeforePropagatingCancellationAsync()
    {
        using CancellationTokenSource cancellation = new();
        using ReorderHarness harness = new(
            Snapshot("22", "80", "443"),
            Snapshot("80", "443"),
            Snapshot("22", "80", "443"));
        harness.EnqueueProcess(exitCode: 0, cancellationRequested: true, onExecute: cancellation.Cancel);
        harness.EnqueueProcess(exitCode: 0);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await harness.Executor.ExecuteAsync(harness.Request([1, 0, 2]), cancellation.Token));

        Assert.HasCount(2, harness.Commands);
        Assert.IsNull(harness.Journal.Entry);
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectsCrossFamilyDesiredOrderBeforeMutationAsync()
    {
        using ReorderHarness harness = new(SnapshotMixedFamilies());

        RuleReorderExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Request([1, 0]),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReorderExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    private static UfwStatusSnapshot Snapshot(params string[] ports)
    {
        string[] rows = ports
            .Select(static (port, index) => $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere")
            .ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static UfwStatusSnapshot SnapshotNoPort(params string[] actions)
    {
        string[] rows = actions
            .Select(static (action, index) => $"[ {index + 1}] Anywhere                   {action} IN    Anywhere")
            .ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static UfwStatusSnapshot SnapshotMixedFamilies() => UfwStatusParser.Parse(UfwStatusFixtures.WithRules(
        "[ 1] 22/tcp                     ALLOW IN    Anywhere",
        "[ 2] 22/tcp (v6)                ALLOW IN    Anywhere (v6)"))!;

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot);

    private sealed class ReorderHarness : IDisposable
    {
        private readonly Queue<FirewallRuleSnapshotReadResult> _snapshots;
        private readonly Queue<ProcessBehavior> _processes = [];
        private readonly Mock<IFirewallRuleSnapshotReader> _snapshotReader = new(MockBehavior.Strict);
        private readonly Mock<IUfwRunner> _ufwRunner = new(MockBehavior.Strict);

        public ReorderHarness(params UfwStatusSnapshot?[] snapshots)
            : this(duplicateBaselineBeforeFirstMove: true, snapshots)
        {
        }

        public ReorderHarness(bool duplicateBaselineBeforeFirstMove, params UfwStatusSnapshot?[] snapshots)
        {
            if (snapshots.Length == 0)
            {
                throw new ArgumentException("At least one baseline snapshot is required.", nameof(snapshots));
            }

            UfwStatusSnapshot?[] reads = duplicateBaselineBeforeFirstMove
                ? [snapshots[0], snapshots[0], .. snapshots[1..]]
                : snapshots;
            _snapshots = new Queue<FirewallRuleSnapshotReadResult>(reads.Select(static snapshot =>
                snapshot is null
                    ? new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("test read failure"), null)
                    : new FirewallRuleSnapshotReadResult(null, snapshot)));
            _snapshotReader
                .Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _snapshots.Dequeue());
            _ufwRunner
                .Setup(runner => runner.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IUfwCommand command, CancellationToken _) => Execute(command));

            UfwRuleCommandRenderer renderer = new();
            Journal = new InMemoryJournal();
            RuleReorderRecoveryCoordinator recovery = new(
                _snapshotReader.Object,
                _ufwRunner.Object,
                renderer,
                Journal,
                new ConsoleLogger());
            Executor = new FirewallReorderExecutor(
                _snapshotReader.Object,
                new RuleReorderPlanner(),
                new RuleReinsertabilityClassifier(renderer),
                recovery,
                Journal,
                _ufwRunner.Object,
                renderer,
                new ConsoleLogger());
        }

        public FirewallReorderExecutor Executor { get; }

        public InMemoryJournal Journal { get; }

        public List<string[]> Commands { get; } = [];

        public RuleReorderExecutionRequest Request(IReadOnlyList<int> desiredOrder)
        {
            FirewallRuleSnapshotReadResult baseline = _snapshots.Peek();
            return new RuleReorderExecutionRequest(
                FirewallRuleSnapshotFingerprint.Compute(FirewallRuleSet.ToListResponse(baseline.Snapshot!)),
                desiredOrder);
        }

        public void EnqueueProcess(
            int exitCode,
            string standardError = "",
            bool cancellationRequested = false,
            Action? onExecute = null,
            Exception? exception = null) =>
            _processes.Enqueue(new ProcessBehavior(exitCode, standardError, cancellationRequested, onExecute, exception));

        public void Dispose()
        {
        }

        private UfwProcessResult Execute(IUfwCommand command)
        {
            ImmutableArray<string> arguments = command.BuildArguments();
            Commands.Add(arguments.ToArray());
            ProcessBehavior behavior = _processes.Count > 0
                ? _processes.Dequeue()
                : new ProcessBehavior(0, string.Empty, false, null, null);
            behavior.OnExecute?.Invoke();
            if (behavior.Exception is not null)
            {
                throw behavior.Exception;
            }
            return new UfwProcessResult(
                behavior.ExitCode,
                string.Empty,
                behavior.StandardError,
                arguments,
                behavior.CancellationRequested);
        }
    }

    private sealed record ProcessBehavior(
        int ExitCode,
        string StandardError,
        bool CancellationRequested,
        Action? OnExecute,
        Exception? Exception);

    private sealed class InMemoryJournal : IReorderRecoveryJournal
    {
        public ReorderRecoveryJournalEntry? Entry { get; private set; }

        public Task<ReorderRecoveryJournalEntry?> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Entry);

        public Task WriteAsync(ReorderRecoveryJournalEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entry = entry;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entry = null;
            return Task.CompletedTask;
        }
    }
}
