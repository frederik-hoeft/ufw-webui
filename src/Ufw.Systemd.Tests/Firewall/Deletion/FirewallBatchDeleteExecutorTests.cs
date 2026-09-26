using Moq;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Deletion;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Deletion;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class FirewallBatchDeleteExecutorTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_MultipleTargets_DeletesDescendingAndReconcilesEveryStepAsync()
    {
        using BatchDeleteHarness harness = new(
            Snapshot("22", "80", "443", "8080"),
            Snapshot("22", "80", "443", "8080"),
            Snapshot("22", "80", "443"),
            Snapshot("22", "80", "443"),
            Snapshot("22", "443"));

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([1, 3]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.Completed, result.Outcome);
        Assert.HasCount(2, result.Operations);
        Assert.IsTrue(result.Operations.All(static operation => operation.Status == RuleBatchDeleteOperationStatus.Deleted));
        CollectionAssert.AreEqual(new[] { 3, 1 }, result.Operations.Select(static operation => operation.OccurrenceId).ToArray());
        CollectionAssert.AreEqual(new[] { "--force", "delete", "4" }, harness.Commands[0]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "2" }, harness.Commands[1]);
        Assert.IsEmpty(result.PendingOccurrenceIds);
        Assert.IsNotNull(result.FinalSnapshot);
        CollectionAssert.AreEqual(new[] { "22", "443" }, result.FinalSnapshot.Rules.Select(static rule => rule.Rule!.DestinationPorts).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_StaleBaseline_PerformsNoMutationAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80"));
        BatchDeleteRulesPayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(ToResponse(Snapshot("22"))),
            OccurrenceIds = [0],
        };

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(payload, TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(result.Operations);
        Assert.IsEmpty(harness.Commands);
        CollectionAssert.AreEqual(new[] { 0 }, result.PendingOccurrenceIds.ToArray());
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutOfRangeTarget_ReturnsPreconditionFailureAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80"));

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([2]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceBeforeFirstMutation_ReturnsStaleBaselineAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80", "443"), Snapshot("443", "80", "22"));

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([1]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(result.Operations);
        Assert.IsEmpty(harness.Commands);
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessFailureWithExpectedDeletedState_ContinuesAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80", "443"), Snapshot("22", "80", "443"), Snapshot("22", "80"));
        harness.EnqueueProcess(exitCode: 1, standardError: "delete reported failure");

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([2]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual(RuleBatchDeleteOperationStatus.DeletedAfterProcessFailure, result.Operations[0].Status);
        StringAssert.Contains(result.Operations[0].Diagnostic, "delete reported failure");
    }

    [TestMethod]
    public async Task ExecuteAsync_TargetStillPresentAfterAttempt_ReturnsKnownPartialOutcomeAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80", "443"), Snapshot("22", "80", "443"), Snapshot("22", "80", "443"));
        harness.EnqueueProcess(exitCode: 1, standardError: "delete rejected");

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([0, 2]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleBatchDeleteOperationStatus.Failed, result.Operations[0].Status);
        CollectionAssert.AreEqual(new[] { 2, 0 }, result.PendingOccurrenceIds.ToArray());
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceAfterConfirmedDeletion_ReturnsPartialOutcomeAsync()
    {
        using BatchDeleteHarness harness = new(
            Snapshot("22", "80", "443", "8080"),
            Snapshot("22", "80", "443", "8080"),
            Snapshot("22", "80", "443"),
            Snapshot("443", "80", "22"));

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([1, 3]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(3, result.Operations[0].OccurrenceId);
        Assert.AreEqual(RuleBatchDeleteOperationStatus.Deleted, result.Operations[0].Status);
        CollectionAssert.AreEqual(new[] { 1 }, result.PendingOccurrenceIds.ToArray());
        Assert.HasCount(1, harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostMutationSnapshotFailure_ReturnsStateUncertainAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80"), Snapshot("22", "80"), null);

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([1]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.StateUncertain, result.Outcome);
        Assert.IsNull(result.FinalSnapshot);
        Assert.HasCount(1, result.Operations);
        Assert.AreEqual(RuleBatchDeleteOperationStatus.StateUncertain, result.Operations[0].Status);
        CollectionAssert.AreEqual(new[] { 1 }, result.PendingOccurrenceIds.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_DuplicateTarget_DefensivelyRejectsBeforeMutationAsync()
    {
        using BatchDeleteHarness harness = new(Snapshot("22", "80"));

        RuleBatchDeleteExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload([1, 1]), TestContext.CancellationToken);

        Assert.AreEqual(RuleBatchDeleteExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    private static UfwStatusSnapshot Snapshot(params string[] ports)
    {
        string[] rows = ports.Select(static (port, index) => $"[ {index + 1}] {port}/tcp                     ALLOW IN    Anywhere").ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot, TestFirewallConfiguration.Enabled);

    private sealed class BatchDeleteHarness : IDisposable
    {
        private readonly Queue<FirewallRuleSnapshotReadResult> _snapshots;
        private readonly Queue<ProcessBehavior> _processes = [];
        private readonly Mock<IFirewallRuleSnapshotReader> _snapshotReader = new(MockBehavior.Strict);
        private readonly Mock<IUfwRunner> _ufwRunner = new(MockBehavior.Strict);

        public BatchDeleteHarness(params UfwStatusSnapshot?[] snapshots)
        {
            if (snapshots.Length == 0 || snapshots[0] is null)
            {
                throw new ArgumentException("A baseline snapshot is required.", nameof(snapshots));
            }

            _snapshots = new Queue<FirewallRuleSnapshotReadResult>(snapshots.Select(static snapshot => snapshot is null
                ? new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("test read failure"), null, null)
                : new FirewallRuleSnapshotReadResult(null, snapshot, TestFirewallConfiguration.Enabled)));
            _snapshotReader.Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _snapshots.Dequeue());
            _ufwRunner.Setup(runner => runner.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((IUfwCommand command, CancellationToken _) => Execute(command));
            Executor = new FirewallBatchDeleteExecutor(_snapshotReader.Object, _ufwRunner.Object, new ConsoleLogger());
        }

        public FirewallBatchDeleteExecutor Executor { get; }

        public List<string[]> Commands { get; } = [];

        public BatchDeleteRulesPayload Payload(IReadOnlyList<int> occurrenceIds)
        {
            FirewallRuleSnapshotReadResult baseline = _snapshots.Peek();
            return new BatchDeleteRulesPayload
            {
                BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(FirewallRuleSet.ToListResponse(baseline.Snapshot!, baseline.Configuration!)),
                OccurrenceIds = [.. occurrenceIds],
            };
        }

        public void EnqueueProcess(int exitCode, string standardError = "", bool cancellationRequested = false, Exception? exception = null) =>
            _processes.Enqueue(new ProcessBehavior(exitCode, standardError, cancellationRequested, exception));

        public void Dispose()
        {
        }

        private UfwProcessResult Execute(IUfwCommand command)
        {
            ImmutableArray<string> arguments = command.BuildArguments();
            Commands.Add(arguments.ToArray());
            ProcessBehavior behavior = _processes.Count > 0 ? _processes.Dequeue() : new ProcessBehavior(0, string.Empty, false, null);
            if (behavior.Exception is not null)
            {
                throw behavior.Exception;
            }
            return new UfwProcessResult(behavior.ExitCode, string.Empty, behavior.StandardError, arguments, behavior.CancellationRequested);
        }
    }

    private sealed record ProcessBehavior(int ExitCode, string StandardError, bool CancellationRequested, Exception? Exception);
}
