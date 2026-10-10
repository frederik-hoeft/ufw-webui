using Moq;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Replacement;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Replacement;

[TestClass]
public sealed class FirewallRuleReplacementServiceTests
{
    private const string NONCE = "replace-nonce";
    private const long EXPIRES_AT_UNIX = 2_000_000_000;

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReplaceAsync_AcceptedIntent_EntersGateChecksSafetyConsumesNonceThenExecutesAsync()
    {
        List<string> calls = [];
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("nonce"))
            .ReturnsAsync(true);
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        gate.Setup(value => value.RunAsync(It.IsAny<Func<CancellationToken, Task<IResponsePayload>>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("gate"))
            .Returns((Func<CancellationToken, Task<IResponsePayload>> action, CancellationToken cancellationToken) => action(cancellationToken));
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("guard"))
            .Returns(Task.CompletedTask);
        Mock<IFirewallRuleReplacementExecutor> executor = new(MockBehavior.Strict);
        executor.Setup(value => value.ExecuteAsync(It.IsAny<ReplaceRulePayload>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("executor"))
            .ReturnsAsync(CompletedResult());
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate.Object, guard.Object, CreateCleanSnapshotReader()), executor.Object);

        IResponsePayload result = await service.ReplaceAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<RuleReplacementResponse>(result);
        CollectionAssert.AreEqual(new List<string> { "gate", "guard", "nonce", "executor" }, calls);
    }

    [TestMethod]
    public async Task ReplaceAsync_RejectedIntent_DoesNotEnterMutationBoundaryAsync()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(value => value.VerifyReplace(It.IsAny<ReplaceRuleRequest>())).Returns(new IntentVerificationResult.Rejected(new ForbiddenResponse("invalid signature")));
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        Mock<IFirewallRuleReplacementExecutor> executor = new(MockBehavior.Strict);
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate.Object, guard.Object, CreateCleanSnapshotReader()), executor.Object);

        IResponsePayload result = await service.ReplaceAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(result);
        nonceStore.VerifyNoOtherCalls();
        gate.VerifyNoOtherCalls();
        guard.VerifyNoOtherCalls();
        executor.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReplaceAsync_OutstandingRecoveryBlocksBeforeNonceConsumptionAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("reorder recovery pending"));
        Mock<IFirewallRuleReplacementExecutor> executor = new(MockBehavior.Strict);
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader()), executor.Object);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            _ = await service.ReplaceAsync(CreateRequest(), TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "recovery");
        nonceStore.VerifyNoOtherCalls();
        executor.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReplaceAsync_ReplayIsRejectedBeforeExecutorRunsAgainAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.SetupSequence(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallRuleReplacementExecutor> executor = CreateExecutor();
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader()), executor.Object);
        ReplaceRuleRequest request = CreateRequest();

        Assert.IsInstanceOfType<RuleReplacementResponse>(await service.ReplaceAsync(request, TestContext.CancellationToken));
        Assert.IsInstanceOfType<ConflictResponse>(await service.ReplaceAsync(request, TestContext.CancellationToken));
        executor.Verify(value => value.ExecuteAsync(It.IsAny<ReplaceRulePayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ReplaceAsync_ConcurrentReplayCrossesExecutionBoundaryAtMostOnceAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        int consumed = 0;
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(() => Interlocked.Increment(ref consumed) == 1);
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallRuleReplacementExecutor> executor = CreateExecutor();
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader()), executor.Object);
        ReplaceRuleRequest request = CreateRequest();

        IResponsePayload[] results = await Task.WhenAll(
            service.ReplaceAsync(request, TestContext.CancellationToken).AsTask(),
            service.ReplaceAsync(request, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, results.Count(static result => result is RuleReplacementResponse));
        Assert.AreEqual(1, results.Count(static result => result is ConflictResponse));
        executor.Verify(value => value.ExecuteAsync(It.IsAny<ReplaceRulePayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(0, RuleReplacementOutcome.Completed, null)]
    [DataRow(1, RuleReplacementOutcome.StaleBaseline, null)]
    [DataRow(2, RuleReplacementOutcome.PreconditionFailed, RuleReplacementRecoveryOutcome.RestoredBaseline)]
    [DataRow(3, RuleReplacementOutcome.PartiallyCompleted, RuleReplacementRecoveryOutcome.Failed)]
    [DataRow(4, RuleReplacementOutcome.StateUncertain, RuleReplacementRecoveryOutcome.Failed)]
    public async Task ReplaceAsync_MapsTypedExecutionOutcomeAsync(int executionOutcomeValue, RuleReplacementOutcome responseOutcome, RuleReplacementRecoveryOutcome? responseRecovery)
    {
        RuleReplacementExecutionOutcome executionOutcome = (RuleReplacementExecutionOutcome)executionOutcomeValue;
        RuleReplacementRecoveryStatus? executionRecovery = responseRecovery switch
        {
            RuleReplacementRecoveryOutcome.RestoredBaseline => RuleReplacementRecoveryStatus.RestoredBaseline,
            RuleReplacementRecoveryOutcome.Failed => RuleReplacementRecoveryStatus.Failed,
            _ => null,
        };
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore();
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallRuleReplacementExecutor> executor = new(MockBehavior.Strict);
        RuleListResponse snapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
        executor.Setup(value => value.ExecuteAsync(It.IsAny<ReplaceRulePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleReplacementExecutionResult(executionOutcome, snapshot, null, executionRecovery, "diagnostic"));
        FirewallRuleReplacementService service = new(verifier.Object, new SignedMutationOrchestrator(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader()), executor.Object);

        IResponsePayload result = await service.ReplaceAsync(CreateRequest(), TestContext.CancellationToken);

        RuleReplacementResponse response = Assert.IsInstanceOfType<RuleReplacementResponse>(result);
        Assert.AreEqual(responseOutcome, response.Outcome);
        Assert.AreEqual(responseRecovery, response.RecoveryOutcome);
        Assert.AreSame(snapshot, response.FinalSnapshot);
        Assert.AreEqual("diagnostic", response.Diagnostic);
    }

    private static Mock<IIntentVerifier> CreateVerifier()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(value => value.VerifyReplace(It.IsAny<ReplaceRuleRequest>())).Returns(new IntentVerificationResult.AcceptedReplacement("key", NONCE, EXPIRES_AT_UNIX, Payload()));
        return verifier;
    }

    private static Mock<INonceStore> CreateNonceStore()
    {
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return nonceStore;
    }

    private static Mock<IFirewallMutationSafetyGuard> CreateSafetyGuard()
    {
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return guard;
    }

    private static Mock<IFirewallRuleReplacementExecutor> CreateExecutor()
    {
        Mock<IFirewallRuleReplacementExecutor> executor = new(MockBehavior.Strict);
        executor.Setup(value => value.ExecuteAsync(It.IsAny<ReplaceRulePayload>(), It.IsAny<CancellationToken>())).ReturnsAsync(CompletedResult());
        return executor;
    }

    private static RuleReplacementExecutionResult CompletedResult() =>
        new(RuleReplacementExecutionOutcome.Completed, new RuleListResponse(Active: true, [], TestFirewallConfiguration.Enabled), null, null, null);

    private static ReplaceRuleRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 1,
        Nonce = NONCE,
        Operation = IntentOperations.REPLACE_RULE,
        Payload = JsonSerializer.SerializeToElement(Payload()),
        Signature = "signature",
    };

    private static ReplaceRulePayload Payload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        TargetOccurrenceId = 0,
        OriginalRuleId = RuleIdentity.Compute(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
        }),
        ReplacementRule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
        },
    };

    private static IFirewallRuleSnapshotReader CreateCleanSnapshotReader()
    {
        Mock<IFirewallRuleSnapshotReader> reader = new();
        reader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirewallRuleSnapshotReadResult.Success(new Ufw.Shared.Ipc.Model.Responses.Domain.RuleListResponse(
                true, [], Ufw.Systemd.Tests.TestFirewallConfiguration.Enabled)));
        return reader.Object;
    }
}
