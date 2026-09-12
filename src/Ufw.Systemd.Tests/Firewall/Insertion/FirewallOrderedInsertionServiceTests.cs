using Moq;
using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Insertion;

namespace Ufw.Systemd.Tests.Firewall.Insertion;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected call order is a local one-shot assertion.")]
public sealed class FirewallOrderedInsertionServiceTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task InsertAsync_RunsSafetyGuardAndExecutorInsideSharedGateAsync()
    {
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        List<string> calls = [];
        gate.Setup(value => value.RunAsync(It.IsAny<Func<CancellationToken, Task<RuleInsertionExecutionResult>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<RuleInsertionExecutionResult>>, CancellationToken>(async (action, cancellationToken) =>
            {
                calls.Add("gate");
                return await action(cancellationToken);
            });
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ =>
            {
                calls.Add("guard");
                return Task.CompletedTask;
            });
        executor.Setup(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()))
            .Returns<InsertRulePayload, CancellationToken>((_, _) =>
            {
                calls.Add("executor");
                return Task.FromResult(new RuleInsertionExecutionResult(RuleInsertionExecutionOutcome.Completed, null, null, null));
            });
        FirewallOrderedInsertionService service = new(gate.Object, guard.Object, executor.Object);

        RuleInsertionExecutionResult result = await service.InsertAsync(Payload(), TestContext.CancellationToken);

        Assert.AreEqual(RuleInsertionExecutionOutcome.Completed, result.Outcome);
        CollectionAssert.AreEqual(new[] { "gate", "guard", "executor" }, calls);
    }

    [TestMethod]
    public async Task InsertAsync_UnsafeMutationState_DoesNotInvokeExecutorAsync()
    {
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        gate.Setup(value => value.RunAsync(It.IsAny<Func<CancellationToken, Task<RuleInsertionExecutionResult>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<RuleInsertionExecutionResult>>, CancellationToken>((action, cancellationToken) => action(cancellationToken));
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("recovery pending"));
        FirewallOrderedInsertionService service = new(gate.Object, guard.Object, executor.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            _ = await service.InsertAsync(Payload(), TestContext.CancellationToken));

        executor.Verify(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static InsertRulePayload Payload() => new()
    {
        BaselineFingerprint = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        AnchorOccurrenceId = 0,
        Placement = RuleInsertionPlacement.Before,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
        },
    };
}
