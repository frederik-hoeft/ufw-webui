using Moq;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Insertion;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Insertion;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class FirewallOrderedInsertionExecutorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_BeforeIpv4Anchor_UsesFamilyLocalPositionAndExactPostconditionAsync()
    {
        using InsertionHarness harness = new(
            Snapshot("80", "443", "80v6"),
            Snapshot("22", "80", "443", "80v6"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(anchorOccurrenceId: 0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        Assert.IsNotNull(result.InsertedRule);
        CollectionAssert.AreEqual(
            new[] { "insert", "1", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            harness.Commands.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterMiddleIpv4Anchor_InsertsBeforeNextFamilyOccurrenceAsync()
    {
        using InsertionHarness harness = new(
            Snapshot("80", "443", "8080", "80v6"),
            Snapshot("80", "443", "22", "8080", "80v6"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(anchorOccurrenceId: 1, RuleInsertionPlacement.After, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(new[] { "insert", "3", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" }, harness.Commands.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterLastIpv4Anchor_AppendsWithinIpv4FamilyAsync()
    {
        using InsertionHarness harness = new(
            Snapshot("80", "443", "80v6", "443v6"),
            Snapshot("80", "443", "22", "80v6", "443v6"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(anchorOccurrenceId: 1, RuleInsertionPlacement.After, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual("allow", harness.Commands.Single()[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterLastIpv4Anchor_AppendsBeforeOpaqueIpv6PartitionAsync()
    {
        using InsertionHarness harness = new(
            SnapshotWithOpaqueIpv6(inserted: false),
            SnapshotWithOpaqueIpv6(inserted: true));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(anchorOccurrenceId: 0, RuleInsertionPlacement.After, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual("allow", harness.Commands.Single()[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_BeforeIpv6Anchor_TranslatesCombinedOccurrenceToFamilyLocalPositionAsync()
    {
        using InsertionHarness harness = new(
            Snapshot("80", "443", "80v6", "443v6"),
            Snapshot("80", "443", "80v6", "22v6", "443v6"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(anchorOccurrenceId: 3, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv6, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "insert", "2", "allow", "in", "from", "::/0", "to", "::/0", "port", "22", "proto", "tcp" },
            harness.Commands.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_StaleBaseline_PerformsNoMutationAsync()
    {
        UfwStatusSnapshot actual = Snapshot("80", "443");
        using InsertionHarness harness = new(actual);
        InsertRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(ToResponse(Snapshot("80"))),
            AnchorOccurrenceId = 0,
            Placement = RuleInsertionPlacement.Before,
            Rule = Rule(FirewallAddressFamily.IPv4, "22"),
        };

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(payload, TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_SemanticDuplicate_PerformsNoMutationAsync()
    {
        using InsertionHarness harness = new(Snapshot("22", "80"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(1, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_InterfaceValidationFailure_PerformsNoMutationAsync()
    {
        using InsertionHarness harness = new(Snapshot("80"));
        harness.InterfaceValidationResponse = new ModelValidationErrorResponse(
            [new ModelValidationError(nameof(FirewallRuleSpecification.SourceInterface), "missing")]);
        FirewallRuleSpecification rule = Rule(FirewallAddressFamily.IPv4, "22");
        rule.SourceInterface = "missing0";

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, rule),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessFailureWithExactTargetState_IsCompletedAsync()
    {
        using InsertionHarness harness = new(Snapshot("80"), Snapshot("22", "80"));
        harness.EnqueueProcess(exitCode: 1, standardError: "reported failure");

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "reported failure");
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessExceptionWithExactTargetState_IsCompletedAsync()
    {
        using InsertionHarness harness = new(Snapshot("80"), Snapshot("22", "80"));
        harness.EnqueueProcess(exception: new ChildProcessException("wait failed", new IOException("inner")));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "wait failed");
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessFailureWithUnchangedState_IsPreconditionFailedAsync()
    {
        using InsertionHarness harness = new(Snapshot("80"), Snapshot("80"));
        harness.EnqueueProcess(exitCode: 1, standardError: "rejected");

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.PreconditionFailed, result.Outcome);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutOfBandDivergenceAfterMutation_IsStateUncertainAsync()
    {
        using InsertionHarness harness = new(Snapshot("80", "443"), Snapshot("22", "443"));

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.StateUncertain, result.Outcome);
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_CanceledProcessWithExactTargetState_IsCompletedAsync()
    {
        using CancellationTokenSource cancellation = new();
        using InsertionHarness harness = new(Snapshot("80"), Snapshot("22", "80"));
        harness.EnqueueProcess(cancellationRequested: true, onExecute: cancellation.Cancel);

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            cancellation.Token);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
    }

    [TestMethod]
    public async Task ExecuteAsync_CanceledProcessWithUnchangedState_PropagatesCancellationAsync()
    {
        using CancellationTokenSource cancellation = new();
        using InsertionHarness harness = new(Snapshot("80"), Snapshot("80"));
        harness.EnqueueProcess(cancellationRequested: true, onExecute: cancellation.Cancel);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await harness.Executor.ExecuteAsync(
                harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
                cancellation.Token));
    }

    [TestMethod]
    public async Task ExecuteAsync_UnreadablePostState_IsStateUncertainAsync()
    {
        using InsertionHarness harness = new(Snapshot("80"), null);

        RuleInsertionExecutionResult result = await harness.Executor.ExecuteAsync(
            harness.Payload(0, RuleInsertionPlacement.Before, Rule(FirewallAddressFamily.IPv4, "22")),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.StateUncertain, result.Outcome);
        Assert.IsNull(result.FinalSnapshot);
    }

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string port) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        Destination = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        DestinationPorts = port,
    };

    private static UfwStatusSnapshot Snapshot(params string[] tokens)
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

    private static UfwStatusSnapshot SnapshotWithOpaqueIpv6(bool inserted) => inserted
        ? UfwStatusParser.Parse(UfwStatusFixtures.WithRules(
            "[ 1] 80/tcp                     ALLOW IN    Anywhere",
            "[ 2] 22/tcp                     ALLOW IN    Anywhere",
            "[ 3] unsupported opaque rule (v6)"))!
        : UfwStatusParser.Parse(UfwStatusFixtures.WithRules(
            "[ 1] 80/tcp                     ALLOW IN    Anywhere",
            "[ 2] unsupported opaque rule (v6)"))!;

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot);

    private sealed class InsertionHarness : IDisposable
    {
        private readonly Queue<FirewallRuleSnapshotReadResult> _snapshots;
        private readonly Queue<ProcessBehavior> _processes = [];
        private readonly Mock<IFirewallRuleSnapshotReader> _snapshotReader = new(MockBehavior.Strict);
        private readonly Mock<IFirewallRuleInterfaceValidator> _interfaceValidator = new(MockBehavior.Strict);
        private readonly Mock<IUfwRunner> _ufwRunner = new(MockBehavior.Strict);
        private IResponsePayload? _interfaceValidationResponse;

        public InsertionHarness(params UfwStatusSnapshot?[] snapshots)
        {
            _snapshots = new Queue<FirewallRuleSnapshotReadResult>(snapshots.Select(static snapshot =>
                snapshot is null
                    ? new FirewallRuleSnapshotReadResult(new InternalServerErrorResponse("test read failure"), null)
                    : new FirewallRuleSnapshotReadResult(null, snapshot)));
            _snapshotReader
                .Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _snapshots.Dequeue());
            _interfaceValidator
                .Setup(validator => validator.Validate(It.IsAny<FirewallRuleSpecification>()))
                .Returns(() => _interfaceValidationResponse);
            _ufwRunner
                .Setup(runner => runner.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IUfwCommand command, CancellationToken _) => Execute(command));

            Executor = new FirewallOrderedInsertionExecutor(
                _snapshotReader.Object,
                _interfaceValidator.Object,
                _ufwRunner.Object,
                new UfwRuleCommandRenderer(),
                new ConsoleLogger());
        }

        public FirewallOrderedInsertionExecutor Executor { get; }

        public List<string[]> Commands { get; } = [];

        public IResponsePayload? InterfaceValidationResponse
        {
            set => _interfaceValidationResponse = value;
        }

        public InsertRulePayload Payload(int anchorOccurrenceId, RuleInsertionPlacement placement, FirewallRuleSpecification rule)
        {
            RuleListResponse baseline = FirewallRuleSet.ToListResponse(_snapshots.Peek().Snapshot!);
            return new InsertRulePayload
            {
                BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
                AnchorOccurrenceId = anchorOccurrenceId,
                Placement = placement,
                Rule = rule,
            };
        }

        public void EnqueueProcess(
            int exitCode = 0,
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
}
