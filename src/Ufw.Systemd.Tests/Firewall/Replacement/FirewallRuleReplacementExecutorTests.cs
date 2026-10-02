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
using Ufw.Systemd.Firewall.Replacement;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Replacement;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class FirewallRuleReplacementExecutorTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_UnchangedRule_CompletesWithoutUfwMutationAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "same"));
        using ReplacementHarness harness = new(baseline);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "80", "same")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        Assert.AreSame(result.FinalSnapshot!.Rules[0], result.ReplacementRule);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_UniqueSameIdentityCommentChange_UsesExistingRuleUpdateAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "old"), RuleToken.V4("443"));
        UfwStatusSnapshot updated = Snapshot(RuleToken.V4("80", "new"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(baseline, updated);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "80", "new")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual("new", result.ReplacementRule!.Rule!.Comment);
        CollectionAssert.AreEqual(
            new[] { "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp", "comment", "new" },
            harness.Commands.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UniqueSameIdentityCommentRemoval_EmitsExplicitEmptyCommentAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "old"));
        UfwStatusSnapshot updated = Snapshot(RuleToken.V4("80"));
        using ReplacementHarness harness = new(baseline, updated);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "80")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "80", "proto", "tcp", "comment", string.Empty },
            harness.Commands.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_UnchangedRuleInDuplicateState_IsRejectedBeforeMutationAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "same"), RuleToken.V4("80", "same"));
        using ReplacementHarness harness = new(baseline);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "80", "same")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameIdentityDuplicateState_IsRejectedBeforeMutationAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "first"), RuleToken.V4("80", "second"));
        using ReplacementHarness harness = new(baseline);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(1, Rule(FirewallAddressFamily.IPv4, "80", "updated")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "occurs more than once");
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_IdentityChangingDuplicateOldState_ReplacesExactOccurrenceWhenNaturallyUnambiguousAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80", "first"), RuleToken.V4("80", "second"), RuleToken.V4("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("80", "first"), RuleToken.V4("22"), RuleToken.V4("80", "second"), RuleToken.V4("443"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("80", "first"), RuleToken.V4("22"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(baseline, intermediate, final);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(1, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual(2, harness.Commands.Count);
        Assert.AreEqual("insert", harness.Commands[0][0]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "3" }, harness.Commands[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplacementIdentityAlreadyExists_IsRejectedBeforeMutationAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("22"));
        using ReplacementHarness harness = new(baseline);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "duplicate");
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_FirstIpv4Replacement_PreservesFirstPositionAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("22"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(baseline, intermediate, final);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual("1", harness.Commands[0][1]);
        Assert.AreEqual("22", result.FinalSnapshot!.Rules[0].Rule!.DestinationPorts);
    }

    [TestMethod]
    public async Task ExecuteAsync_LastIpv4Replacement_PreservesLastPositionAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("80"), RuleToken.V4("22"), RuleToken.V4("443"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("80"), RuleToken.V4("22"));
        using ReplacementHarness harness = new(baseline, intermediate, final);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(1, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        Assert.AreEqual("2", harness.Commands[0][1]);
        Assert.AreEqual("22", result.FinalSnapshot!.Rules[1].Rule!.DestinationPorts);
    }

    [TestMethod]
    public async Task ExecuteAsync_MiddleIpv4Replacement_InsertsBeforeTargetThenDeletesShiftedOriginalAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"), RuleToken.V4("8080"), RuleToken.V6("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("80"), RuleToken.V4("22"), RuleToken.V4("443"), RuleToken.V4("8080"), RuleToken.V6("80"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("80"), RuleToken.V4("22"), RuleToken.V4("8080"), RuleToken.V6("80"));
        using ReplacementHarness harness = new(baseline, intermediate, final);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(1, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "insert", "2", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" },
            harness.Commands[0]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "3" }, harness.Commands[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Ipv6Replacement_UsesCombinedUfwPositionAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"), RuleToken.V6("80"), RuleToken.V6("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"), RuleToken.V6("80"), RuleToken.V6("22"), RuleToken.V6("443"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"), RuleToken.V6("80"), RuleToken.V6("22"));
        using ReplacementHarness harness = new(baseline, intermediate, final);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(3, Rule(FirewallAddressFamily.IPv6, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "insert", "4", "allow", "in", "from", "::/0", "to", "::/0", "port", "22", "proto", "tcp" },
            harness.Commands[0]);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "5" }, harness.Commands[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_StaleBaseline_PerformsNoMutationAsync()
    {
        UfwStatusSnapshot actual = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(actual);
        ReplaceRulePayload payload = harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22"));
        payload.BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(ToResponse(Snapshot(RuleToken.V4("80"))));

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(payload, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.StaleBaseline, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_TargetIdentityMismatch_IsRejectedBeforeMutationAsync()
    {
        using ReplacementHarness harness = new(Snapshot(RuleToken.V4("80")));
        ReplaceRulePayload payload = harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22"));
        payload.OriginalRuleId = RuleIdentity.Compute(Rule(FirewallAddressFamily.IPv4, "9999"));

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(payload, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_InterfaceValidationFailure_PerformsNoMutationAsync()
    {
        using ReplacementHarness harness = new(Snapshot(RuleToken.V4("80")));
        harness.InterfaceValidationResponse = new ModelValidationErrorResponse([new ModelValidationError(nameof(FirewallRuleSpecification.SourceInterface), "missing")]);
        FirewallRuleSpecification replacement = Rule(FirewallAddressFamily.IPv4, "22");
        replacement.SourceInterface = "missing0";

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, replacement), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_Ipv6DisabledRejectsIpv6ReplacementWithoutMutationAsync()
    {
        using ReplacementHarness harness = new(TestFirewallConfiguration.Disabled, Snapshot(RuleToken.V6("80")));

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv6, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsEmpty(harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_InsertionFailureWithUnchangedState_IsPreconditionFailedAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        using ReplacementHarness harness = new(baseline, baseline);
        harness.EnqueueProcess(exitCode: 1, standardError: "rejected");

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.IsNull(result.RecoveryStatus);
    }

    [TestMethod]
    public async Task ExecuteAsync_InsertionProcessFailureWithConfirmedIntermediate_ContinuesAndCompletesAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("22"));
        using ReplacementHarness harness = new(baseline, intermediate, final);
        harness.EnqueueProcess(exitCode: 1, standardError: "insert reported failure");

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "insert reported failure");
        Assert.AreEqual(2, harness.Commands.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_DeleteProcessFailureWithConfirmedFinalState_IsCompletedAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"));
        UfwStatusSnapshot final = Snapshot(RuleToken.V4("22"));
        using ReplacementHarness harness = new(baseline, intermediate, final);
        harness.EnqueueProcess();
        harness.EnqueueProcess(exitCode: 1, standardError: "delete reported failure");

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.Completed, result.Outcome);
        StringAssert.Contains(result.Diagnostic, "delete reported failure");
    }

    [TestMethod]
    public async Task ExecuteAsync_DeleteFailureWithExactIntermediate_RollsBackToBaselineAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(baseline, intermediate, intermediate, baseline);
        harness.EnqueueProcess();
        harness.EnqueueProcess(exitCode: 1, standardError: "delete failed");
        harness.EnqueueProcess();

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PreconditionFailed, result.Outcome);
        Assert.AreEqual(RuleReplacementRecoveryStatus.RestoredBaseline, result.RecoveryStatus);
        Assert.AreEqual(3, harness.Commands.Count);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CanceledDeleteWithExactIntermediate_RestoresBaselineBeforePropagatingAsync()
    {
        using CancellationTokenSource cancellation = new();
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"));
        using ReplacementHarness harness = new(baseline, intermediate, intermediate, baseline);
        harness.EnqueueProcess();
        harness.EnqueueProcess(cancellationRequested: true, onExecute: cancellation.Cancel);
        harness.EnqueueProcess();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), cancellation.Token));

        Assert.AreEqual(3, harness.Commands.Count);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_DeleteFailureAndRollbackFailureLeavingExactIntermediate_IsPartialAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"));
        using ReplacementHarness harness = new(baseline, intermediate, intermediate, intermediate);
        harness.EnqueueProcess();
        harness.EnqueueProcess(exitCode: 1, standardError: "delete failed");
        harness.EnqueueProcess(exitCode: 1, standardError: "rollback failed");

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.PartiallyCompleted, result.Outcome);
        Assert.AreEqual(RuleReplacementRecoveryStatus.Failed, result.RecoveryStatus);
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceAfterDelete_StopsWithoutRollbackAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot divergent = Snapshot(RuleToken.V4("22"), RuleToken.V4("8080"));
        using ReplacementHarness harness = new(baseline, intermediate, divergent);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.StateUncertain, result.Outcome);
        Assert.IsNull(result.RecoveryStatus);
        Assert.AreEqual(2, harness.Commands.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_DivergenceAfterInsertion_StopsWithoutDeleteOrRollbackAsync()
    {
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"), RuleToken.V4("443"));
        UfwStatusSnapshot divergent = Snapshot(RuleToken.V4("22"), RuleToken.V4("8080"), RuleToken.V4("443"));
        using ReplacementHarness harness = new(baseline, divergent);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.StateUncertain, result.Outcome);
        Assert.HasCount(1, harness.Commands);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnreadableStateAfterInsertion_IsStateUncertainAsync()
    {
        using ReplacementHarness harness = new(Snapshot(RuleToken.V4("80")), null);

        RuleReplacementExecutionResult result = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementExecutionOutcome.StateUncertain, result.Outcome);
        Assert.IsNull(result.FinalSnapshot);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationAfterConfirmedInsertion_RestoresBaselineBeforePropagatingAsync()
    {
        using CancellationTokenSource cancellation = new();
        UfwStatusSnapshot baseline = Snapshot(RuleToken.V4("80"));
        UfwStatusSnapshot intermediate = Snapshot(RuleToken.V4("22"), RuleToken.V4("80"));
        using ReplacementHarness harness = new(baseline, intermediate, baseline);
        harness.EnqueueProcess(cancellationRequested: true, onExecute: cancellation.Cancel);
        harness.EnqueueProcess();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await harness.Executor.ExecuteAsync(harness.Payload(0, Rule(FirewallAddressFamily.IPv4, "22")), cancellation.Token));

        Assert.AreEqual(2, harness.Commands.Count);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "1" }, harness.Commands[1]);
    }

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string port, string? comment = null) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        Destination = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        DestinationPorts = port,
        Comment = comment,
    };

    private static UfwStatusSnapshot Snapshot(params RuleToken[] tokens)
    {
        string[] rows = tokens.Select(static (token, index) => token.Family == FirewallAddressFamily.IPv6
            ? $"[{index + 1,2}] {token.Port}/tcp (v6)                ALLOW IN    Anywhere (v6){FormatComment(token.Comment)}"
            : $"[{index + 1,2}] {token.Port}/tcp                     ALLOW IN    Anywhere{FormatComment(token.Comment)}").ToArray();
        return UfwStatusParser.Parse(UfwStatusFixtures.WithRules(rows))!;
    }

    private static string FormatComment(string? comment) => comment is null ? string.Empty : $"                   # {comment}";

    private static RuleListResponse ToResponse(UfwStatusSnapshot snapshot) => FirewallRuleSet.ToListResponse(snapshot, TestFirewallConfiguration.Enabled);

    private sealed record RuleToken(FirewallAddressFamily Family, string Port, string? Comment)
    {
        public static RuleToken V4(string port, string? comment = null) => new(FirewallAddressFamily.IPv4, port, comment);

        public static RuleToken V6(string port, string? comment = null) => new(FirewallAddressFamily.IPv6, port, comment);
    }

    private sealed class ReplacementHarness : IDisposable
    {
        private readonly Queue<FirewallRuleSnapshotReadResult> _snapshots;
        private readonly Queue<ProcessBehavior> _processes = [];
        private readonly Mock<IFirewallRuleSnapshotReader> _snapshotReader = new(MockBehavior.Strict);
        private readonly Mock<IFirewallRuleInterfaceValidator> _interfaceValidator = new(MockBehavior.Strict);
        private readonly Mock<IUfwRunner> _ufwRunner = new(MockBehavior.Strict);
        private IResponsePayload? _interfaceValidationResponse;

        public ReplacementHarness(params UfwStatusSnapshot?[] snapshots)
            : this(TestFirewallConfiguration.Enabled, snapshots)
        {
        }

        public ReplacementHarness(FirewallConfigurationSnapshot configuration, params UfwStatusSnapshot?[] snapshots)
        {
            _snapshots = new Queue<FirewallRuleSnapshotReadResult>(snapshots.Select(snapshot => snapshot is null
                ? (FirewallRuleSnapshotReadResult)new FirewallRuleSnapshotReadResult.Failure(new InternalServerErrorResponse("test read failure"))
                : new FirewallRuleSnapshotReadResult.Success(FirewallRuleSet.ToListResponse(snapshot, configuration))));
            _snapshotReader.Setup(reader => reader.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _snapshots.Dequeue());
            _interfaceValidator.Setup(validator => validator.Validate(It.IsAny<FirewallRuleSpecification>())).Returns(() => _interfaceValidationResponse);
            _ufwRunner.Setup(runner => runner.ExecuteAsync(It.IsAny<IUfwCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync((IUfwCommand command, CancellationToken _) => Execute(command));

            Executor = new FirewallRuleReplacementExecutor(
                _snapshotReader.Object,
                _interfaceValidator.Object,
                new FirewallRuleCapabilityValidator(),
                new UfwProcessExecutor(_ufwRunner.Object, new ConsoleLogger()),
                new UfwRuleCommandRenderer(),
                new ConsoleLogger());
        }

        public FirewallRuleReplacementExecutor Executor { get; }

        public List<string[]> Commands { get; } = [];

        public IResponsePayload? InterfaceValidationResponse
        {
            set => _interfaceValidationResponse = value;
        }

        public ReplaceRulePayload Payload(int targetOccurrenceId, FirewallRuleSpecification replacement)
        {
            RuleListResponse baseline = _snapshots.Peek().GetRequiredSnapshot();
            return new ReplaceRulePayload
            {
                BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
                TargetOccurrenceId = targetOccurrenceId,
                OriginalRuleId = baseline.Rules[targetOccurrenceId].RuleId!,
                ReplacementRule = replacement,
            };
        }

        public void EnqueueProcess(int exitCode = 0, string standardError = "", bool cancellationRequested = false, Action? onExecute = null, Exception? exception = null) =>
            _processes.Enqueue(new ProcessBehavior(exitCode, standardError, cancellationRequested, onExecute, exception));

        public void Dispose()
        {
        }

        private UfwProcessResult Execute(IUfwCommand command)
        {
            ImmutableArray<string> arguments = command.BuildArguments();
            Commands.Add(arguments.ToArray());
            ProcessBehavior behavior = _processes.Count > 0 ? _processes.Dequeue() : new ProcessBehavior(0, string.Empty, false, null, null);
            behavior.OnExecute?.Invoke();
            if (behavior.Exception is not null)
            {
                throw behavior.Exception;
            }
            return new UfwProcessResult(behavior.ExitCode, string.Empty, behavior.StandardError, arguments, behavior.CancellationRequested);
        }
    }

    private sealed record ProcessBehavior(int ExitCode, string StandardError, bool CancellationRequested, Action? OnExecute, Exception? Exception);
}
