using Moq;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Deletion;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;
using ResponseOperationOutcome = Ufw.Shared.Ipc.Model.Responses.Domain.RuleBatchDeleteOperationOutcome;

namespace Ufw.Systemd.Tests.Firewall.Deletion;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected arrays are local one-shot test assertions.")]
public sealed class FirewallBatchDeleteServiceTests
{
    private const string NONCE = "test-nonce";
    private const long EXPIRES_AT_UNIX = 2_000_000_000;

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task DeleteAsync_HoldsSharedExecutionGateForEntireExecutorCallAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore();
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallBatchDeleteExecutor> executor = new(MockBehavior.Strict);
        TaskCompletionSource executorEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseExecutor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        executor.Setup(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>())).Returns(async () =>
        {
            executorEntered.TrySetResult();
            await releaseExecutor.Task;
            return CompletedResult();
        });
        FirewallBatchDeleteService service = new(verifier.Object, nonceStore.Object, gate, safetyGuard.Object, executor.Object);

        Task<IResponsePayload> deletion = service.DeleteAsync(CreateRequest(), TestContext.CancellationToken).AsTask();
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
        Assert.IsInstanceOfType<RuleBatchDeleteResponse>(await deletion);
        Assert.IsTrue(await competing);
        Assert.IsTrue(competingEntered);
    }

    [TestMethod]
    public async Task DeleteAsync_ReplayIsRejectedBeforeExecutorRunsAgainAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.SetupSequence(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallBatchDeleteExecutor> executor = new(MockBehavior.Strict);
        executor.Setup(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>())).ReturnsAsync(CompletedResult());
        FirewallBatchDeleteService service = new(verifier.Object, nonceStore.Object, gate, safetyGuard.Object, executor.Object);
        BatchDeleteRulesRequest request = CreateRequest();

        Assert.IsInstanceOfType<RuleBatchDeleteResponse>(await service.DeleteAsync(request, TestContext.CancellationToken));
        Assert.IsInstanceOfType<ConflictResponse>(await service.DeleteAsync(request, TestContext.CancellationToken));
        executor.Verify(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_ConcurrentReplayCrossesExecutionBoundaryAtMostOnceAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        int consumed = 0;
        nonceStore.Setup(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(() => Interlocked.Increment(ref consumed) == 1);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallBatchDeleteExecutor> executor = new(MockBehavior.Strict);
        executor.Setup(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>())).ReturnsAsync(CompletedResult());
        FirewallBatchDeleteService service = new(verifier.Object, nonceStore.Object, gate, safetyGuard.Object, executor.Object);
        BatchDeleteRulesRequest request = CreateRequest();

        IResponsePayload[] results = await Task.WhenAll(
            service.DeleteAsync(request, TestContext.CancellationToken).AsTask(),
            service.DeleteAsync(request, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, results.Count(static result => result is RuleBatchDeleteResponse));
        Assert.AreEqual(1, results.Count(static result => result is ConflictResponse));
        executor.Verify(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_RejectedIntentDoesNotConsumeNonceOrEnterMutationBoundaryAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(candidate => candidate.VerifyBatchDelete(It.IsAny<BatchDeleteRulesRequest>())).Returns(new IntentVerificationResult.Rejected(new ForbiddenResponse("invalid signature")));
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> safetyGuard = new(MockBehavior.Strict);
        Mock<IFirewallBatchDeleteExecutor> executor = new(MockBehavior.Strict);
        FirewallBatchDeleteService service = new(verifier.Object, nonceStore.Object, gate, safetyGuard.Object, executor.Object);

        IResponsePayload response = await service.DeleteAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(response);
        nonceStore.VerifyNoOtherCalls();
        safetyGuard.VerifyNoOtherCalls();
        executor.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DeleteAsync_MapsStructuredPartialExecutionReportAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore();
        Mock<IFirewallMutationSafetyGuard> safetyGuard = CreateSafetyGuard();
        Mock<IFirewallBatchDeleteExecutor> executor = new(MockBehavior.Strict);
        RuleListResponse finalSnapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
        executor.Setup(candidate => candidate.ExecuteAsync(It.IsAny<BatchDeleteRulesPayload>(), It.IsAny<CancellationToken>())).ReturnsAsync(
            new RuleBatchDeleteExecutionResult(
                RuleBatchDeleteExecutionOutcome.PartiallyCompleted,
                finalSnapshot,
                [new RuleBatchDeleteOperationReport(3, "sha256:rule", RuleBatchDeleteOperationStatus.DeletedAfterProcessFailure, "process failed")],
                [1],
                "stopped"));
        FirewallBatchDeleteService service = new(verifier.Object, nonceStore.Object, gate, safetyGuard.Object, executor.Object);

        RuleBatchDeleteResponse response = Assert.IsInstanceOfType<RuleBatchDeleteResponse>(await service.DeleteAsync(CreateRequest(), TestContext.CancellationToken));

        Assert.AreEqual(RuleBatchDeleteOutcome.PartiallyCompleted, response.Outcome);
        Assert.AreSame(finalSnapshot, response.FinalSnapshot);
        Assert.AreEqual(ResponseOperationOutcome.DeletedAfterProcessFailure, response.Operations[0].Outcome);
        Assert.AreEqual("sha256:rule", response.Operations[0].RuleId);
        CollectionAssert.AreEqual(new[] { 1 }, response.PendingOccurrenceIds.ToArray());
        Assert.AreEqual("stopped", response.Diagnostic);
    }

    private static Mock<IIntentVerifier> CreateVerifier()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(candidate => candidate.VerifyBatchDelete(It.IsAny<BatchDeleteRulesRequest>())).Returns(
            new IntentVerificationResult.AcceptedBatchDelete("key", NONCE, EXPIRES_AT_UNIX, CreatePayload()));
        return verifier;
    }

    private static Mock<INonceStore> CreateNonceStore()
    {
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(store => store.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return nonceStore;
    }

    private static Mock<IFirewallMutationSafetyGuard> CreateSafetyGuard()
    {
        Mock<IFirewallMutationSafetyGuard> safetyGuard = new(MockBehavior.Strict);
        safetyGuard.Setup(candidate => candidate.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return safetyGuard;
    }

    private static RuleBatchDeleteExecutionResult CompletedResult() => new(
        RuleBatchDeleteExecutionOutcome.Completed,
        new RuleListResponse(Active: true, [], TestFirewallConfiguration.Enabled),
        [],
        [],
        null);

    private static BatchDeleteRulesRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 1,
        Nonce = NONCE,
        Operation = IntentOperations.DELETE_RULES_BATCH,
        Payload = JsonSerializer.SerializeToElement(CreatePayload()),
        Signature = "signature",
    };

    private static BatchDeleteRulesPayload CreatePayload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        OccurrenceIds = [1, 3],
    };
}
