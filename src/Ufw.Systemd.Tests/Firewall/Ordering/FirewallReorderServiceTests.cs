using Moq;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;
using ExecutionOperationStatus = Ufw.Systemd.Firewall.Ordering.RuleReorderOperationStatus;
using ResponseOperationOutcome = Ufw.Shared.Ipc.Model.Responses.Domain.RuleReorderOperationOutcome;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FirewallReorderServiceTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReorderAsync_HoldsSharedExecutionGateForEntireExecutorCallAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore(consume: true);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        TaskCompletionSource executorEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseExecutor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        executor
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executorEntered.TrySetResult();
                await releaseExecutor.Task;
                return CompletedResult();
            });
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);

        Task<IResponsePayload> reorder = service.ReorderAsync(CreateRequest(), TestContext.CancellationToken).AsTask();
        await executorEntered.Task.WaitAsync(TestContext.CancellationToken);
        bool competingEntered = false;
        Task<bool> competing = gate.RunAsync(_ =>
        {
            competingEntered = true;
            return Task.FromResult(true);
        }, TestContext.CancellationToken);

        await Task.Delay(20, TestContext.CancellationToken);
        Assert.IsFalse(competingEntered);
        releaseExecutor.TrySetResult();
        Assert.IsInstanceOfType<RuleReorderResponse>(await reorder);
        Assert.IsTrue(await competing);
        Assert.IsTrue(competingEntered);
    }

    [TestMethod]
    public async Task ReorderAsync_ReplayIsRejectedBeforeExecutorRunsAgainAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore
            .SetupSequence(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        executor
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);
        ReorderRulesRequest request = CreateRequest();

        Assert.IsInstanceOfType<RuleReorderResponse>(await service.ReorderAsync(request, TestContext.CancellationToken));
        Assert.IsInstanceOfType<ConflictResponse>(await service.ReorderAsync(request, TestContext.CancellationToken));
        executor.Verify(
            candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ReorderAsync_ConcurrentReplayCrossesExecutionBoundaryAtMostOnceAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore(consume: true);
        int consumed = 0;
        nonceStore
            .Setup(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref consumed) == 1);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        executor
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);
        ReorderRulesRequest request = CreateRequest();

        IResponsePayload[] results = await Task.WhenAll(
            service.ReorderAsync(request, TestContext.CancellationToken).AsTask(),
            service.ReorderAsync(request, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, results.Count(static result => result is RuleReorderResponse));
        Assert.AreEqual(1, results.Count(static result => result is ConflictResponse));
        executor.Verify(
            candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ReorderAsync_ReplayIsRejectedAfterNonceStoreRestartAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-reorder-nonce-tests", Guid.NewGuid().ToString("N"));
        string noncePath = Path.Combine(directory, "nonces");
        Directory.CreateDirectory(directory);
        try
        {
            TestTimeProvider clock = new(DateTimeOffset.FromUnixTimeSeconds(EXPIRES_AT_UNIX - 100));
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: noncePath));
            using UfwExecutionGate gate = new();
            Mock<IIntentVerifier> verifier = CreateVerifier();
            Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
            Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
            executor
                .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CompletedResult());
            ReorderRulesRequest request = CreateRequest();

            using (FileNonceStore firstStore = new(configuration, clock))
            {
                FirewallReorderService firstService = new(
                    verifier.Object,
                    firstStore,
                    gate,
                    safetyGuard.Object,
                    executor.Object);
                Assert.IsInstanceOfType<RuleReorderResponse>(
                    await firstService.ReorderAsync(request, TestContext.CancellationToken));
            }

            using FileNonceStore restartedStore = new(configuration, clock);
            FirewallReorderService restartedService = new(
                verifier.Object,
                restartedStore,
                gate,
                safetyGuard.Object,
                executor.Object);
            Assert.IsInstanceOfType<ConflictResponse>(
                await restartedService.ReorderAsync(request, TestContext.CancellationToken));
            executor.Verify(
                candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReorderAsync_StaleBaselineReturnsStructuredNoMutationReportAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore(consume: true);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        RuleListResponse authoritativeSnapshot = new(Active: true, []);
        executor
            .Setup(candidate => candidate.ExecuteAsync(
                It.Is<RuleReorderExecutionRequest>(request =>
                    request.BaselineFingerprint == CreatePayload().BaselineFingerprint
                    && request.DesiredOrder.SequenceEqual(CreatePayload().DesiredOrder)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleReorderExecutionResult(
                RuleReorderExecutionOutcome.StaleBaseline,
                authoritativeSnapshot,
                [],
                [],
                [],
                "stale"));
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);

        IResponsePayload payload = await service.ReorderAsync(CreateRequest(), TestContext.CancellationToken);

        RuleReorderResponse response = Assert.IsInstanceOfType<RuleReorderResponse>(payload);
        Assert.AreEqual(RuleReorderOutcome.StaleBaseline, response.Outcome);
        Assert.AreSame(authoritativeSnapshot, response.FinalSnapshot);
        Assert.IsEmpty(response.Operations);
        Assert.IsEmpty(response.BlockedOperations);
        Assert.IsEmpty(response.PendingOperations);
        Assert.AreEqual("stale", response.Diagnostic);
    }

    [TestMethod]
    public async Task ReorderAsync_RejectedIntentDoesNotConsumeNonceOrEnterMutationBoundaryAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier
            .Setup(candidate => candidate.VerifyReorder(It.IsAny<ReorderRulesRequest>()))
            .Returns(new IntentVerificationResult.Rejected(new ForbiddenResponse("invalid signature")));
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = new(MockBehavior.Strict);
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);

        IResponsePayload response = await service.ReorderAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(response);
        nonceStore.VerifyNoOtherCalls();
        safetyGuard.VerifyNoOtherCalls();
        executor.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReorderAsync_MapsStructuredPartialExecutionReportAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore(consume: true);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        RuleListResponse finalSnapshot = new(Active: true, []);
        RuleReorderMove appliedMove = new(2, 0, 0);
        RuleReorderMove blockedMove = new(1, 2, null);
        RuleReorderExecutionResult executionResult = new(
            RuleReorderExecutionOutcome.PartiallyCompleted,
            finalSnapshot,
            [new RuleReorderOperationReport(appliedMove, ExecutionOperationStatus.FailedAndRestored, "restored")],
            [blockedMove],
            [blockedMove],
            "state diverged");
        executor
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(executionResult);
        FirewallReorderService service = new(
            verifier.Object,
            nonceStore.Object,
            gate,
            safetyGuard.Object,
            executor.Object);

        IResponsePayload payload = await service.ReorderAsync(CreateRequest(), TestContext.CancellationToken);

        RuleReorderResponse response = Assert.IsInstanceOfType<RuleReorderResponse>(payload);
        Assert.AreEqual(RuleReorderOutcome.PartiallyCompleted, response.Outcome);
        Assert.AreSame(finalSnapshot, response.FinalSnapshot);
        Assert.AreEqual(1, response.Operations.Length);
        Assert.AreEqual(ResponseOperationOutcome.FailedAndRestored, response.Operations[0].Outcome);
        Assert.AreEqual("restored", response.Operations[0].Diagnostic);
        Assert.AreEqual(2, response.Operations[0].Move.OccurrenceId);
        Assert.AreEqual(1, response.BlockedOperations.Length);
        Assert.AreEqual(1, response.PendingOperations.Length);
        Assert.AreEqual("state diverged", response.Diagnostic);
    }

    private const string NONCE = "test-nonce";
    private const long EXPIRES_AT_UNIX = 2_000_000_000;

    private static Mock<IIntentVerifier> CreateVerifier()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier
            .Setup(candidate => candidate.VerifyReorder(It.IsAny<ReorderRulesRequest>()))
            .Returns(new IntentVerificationResult.AcceptedReorder(
                "key",
                NONCE,
                EXPIRES_AT_UNIX,
                CreatePayload()));
        return verifier;
    }

    private static Mock<INonceStore> CreateNonceStore(bool consume)
    {
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore
            .Setup(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consume);
        return nonceStore;
    }

    private static Mock<IFirewallMutationSafetyGuard> CreateSafetyGuard()
    {
        Mock<IFirewallMutationSafetyGuard> safetyGuard = new(MockBehavior.Strict);
        safetyGuard
            .Setup(candidate => candidate.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return safetyGuard;
    }

    private static RuleReorderExecutionResult CompletedResult() => new(
        RuleReorderExecutionOutcome.Completed,
        new RuleListResponse(Active: true, []),
        [],
        [],
        [],
        null);

    private static ReorderRulesRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 1,
        Nonce = NONCE,
        Operation = IntentOperations.REORDER_RULES,
        Payload = JsonSerializer.SerializeToElement(CreatePayload()),
        Signature = "signature",
    };

    private static ReorderRulesPayload CreatePayload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        DesiredOrder = [0],
    };
}
