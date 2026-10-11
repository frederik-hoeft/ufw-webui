using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Replacement;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules.Replacement;

[TestClass]
public sealed class RuleReplacementWorkflowServiceTests
{
    [TestMethod]
    public async Task ReplaceAsync_AfterSuccessfulReconciliationWritesMetadataForConfirmedIdentityAsync()
    {
        using CancellationTokenSource lifetime = new();
        TestHost host = new();
        RuleSnapshot baseline = Snapshot();
        RuleReplacementNavigationContext context = Context(baseline);
        RuleReplacementMutationResponse response = Response(RuleReplacementOutcome.Completed, RuleReplacementMetadataReconciliationOutcome.Completed);
        RuleMetadataChange previous = new("old", [], null);
        RuleMetadataChange desired = new("new", [], null);
        List<string> steps = [];
        host.Mutations.Setup(service => service.ReplaceRuleAsync(It.Is<RuleListResponse>(r => r.Assessment == baseline.Assessment), 0, "original",
            It.IsAny<FirewallRuleSpecification>(), "key", lifetime.Token))
            .Callback(() => steps.Add("firewall")).ReturnsAsync(response);
        host.Metadata.Setup(service => service.UpdateAsync("replacement", desired, lifetime.Token))
            .Callback(() => steps.Add("metadata")).ReturnsAsync(new RuleMetadataMutationResponse());

        RuleEditWorkflowState state = await host.Service.ReplaceAsync(baseline, context, Specification(), previous, desired, "key", lifetime.Token);

        Assert.IsTrue(state.FirewallCompleted);
        Assert.AreEqual("replacement", state.ConfirmedRuleId);
        Assert.IsNull(state.MetadataSaveDiagnostic);
        Assert.IsFalse(state.CanRetryMetadataSave);
        CollectionAssert.AreEqual(new[] { "firewall", "metadata" }, steps);
    }

    [TestMethod]
    public async Task ReplaceAsync_UnchangedNormalizedMetadataSkipsWriteAsync()
    {
        TestHost host = new();
        host.Mutations.Setup(service => service.ReplaceRuleAsync(It.IsAny<RuleListResponse>(), 0, "original",
            It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(RuleReplacementOutcome.Completed, RuleReplacementMetadataReconciliationOutcome.Completed));

        RuleEditWorkflowState state = await host.Service.ReplaceAsync(Snapshot(), Context(Snapshot()), Specification(),
            new RuleMetadataChange(" note ", [Guid.Empty, Guid.Empty], null), new RuleMetadataChange("note", [Guid.Empty], null), "key");

        Assert.IsTrue(state.FirewallCompleted);
        Assert.IsNull(state.MetadataSaveDiagnostic);
        host.Metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(RuleReplacementOutcome.StaleBaseline, RuleReplacementMetadataReconciliationOutcome.NotAttempted)]
    [DataRow(RuleReplacementOutcome.Completed, RuleReplacementMetadataReconciliationOutcome.Failed)]
    public async Task ReplaceAsync_WithoutConfirmedReconciliationNeverWritesMetadataAsync(
        RuleReplacementOutcome outcome, RuleReplacementMetadataReconciliationOutcome reconciliation)
    {
        TestHost host = new();
        host.Mutations.Setup(service => service.ReplaceRuleAsync(It.IsAny<RuleListResponse>(), 0, "original",
            It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(Response(outcome, reconciliation));

        RuleEditWorkflowState state = await host.Service.ReplaceAsync(Snapshot(), Context(Snapshot()), Specification(),
            new RuleMetadataChange(null, [], null), new RuleMetadataChange("change", [], null), "key");

        Assert.AreEqual(outcome == RuleReplacementOutcome.Completed, state.FirewallCompleted);
        Assert.IsFalse(state.CanRetryMetadataSave);
        host.Metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ReplaceAsync_MetadataFailurePreservesCompletedFirewallAndRetrySkipsMutationAsync()
    {
        TestHost host = new();
        RuleMetadataChange previous = new("old", [], null);
        RuleMetadataChange desired = new("new", [], null);
        host.Mutations.Setup(service => service.ReplaceRuleAsync(It.IsAny<RuleListResponse>(), 0, "original",
            It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(RuleReplacementOutcome.Completed, RuleReplacementMetadataReconciliationOutcome.Completed));
        host.Metadata.SetupSequence(service => service.UpdateAsync("replacement", desired, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(new RuleMetadataMutationResponse());
        host.Errors.Setup(mapper => mapper.Describe(It.IsAny<HttpRequestException>()))
            .Returns(new ClientError(ClientErrorKind.Unavailable, "retry metadata", Retryable: true));

        RuleEditWorkflowState state = await host.Service.ReplaceAsync(Snapshot(), Context(Snapshot()), Specification(), previous, desired, "key");

        Assert.IsTrue(state.FirewallCompleted);
        Assert.IsTrue(state.CanRetryMetadataSave);
        Assert.AreEqual("retry metadata", state.MetadataSaveDiagnostic);
        RuleEditWorkflowState retried = await host.Service.RetryMetadataAsync(state, previous, desired);
        Assert.IsFalse(retried.CanRetryMetadataSave);
        Assert.IsNull(retried.MetadataSaveDiagnostic);
        host.Mutations.Verify(service => service.ReplaceRuleAsync(It.IsAny<RuleListResponse>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<FirewallRuleSpecification>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RetryMetadataAsync_RequiresPreviousFailureAsync()
    {
        TestHost host = new();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.Service.RetryMetadataAsync(
            RuleEditWorkflowState.Initial, new RuleMetadataChange(null, [], null), new RuleMetadataChange(null, [], null)));
        host.Mutations.VerifyNoOtherCalls();
        host.Metadata.VerifyNoOtherCalls();
    }

    private static FirewallRuleSpecification Specification() => new() { Action = FirewallAction.Allow, AddressFamily = FirewallAddressFamily.IPv4 };

    private static ListedFirewallRule Rule(string ruleId) => new() { RuleId = ruleId, Parsed = true, Rule = Specification() };

    private static RuleSnapshot Snapshot() => new(true, [Rule("original")], TestFirewallConfiguration.Enabled);

    private static RuleReplacementNavigationContext Context(RuleSnapshot snapshot) => new("baseline", 0, 1, "original", FirewallAddressFamily.IPv4, snapshot.Rules[0]);

    private static RuleReplacementMutationResponse Response(RuleReplacementOutcome outcome, RuleReplacementMetadataReconciliationOutcome reconciliation)
    {
        RuleReplacementResponse firewall = new(outcome, new RuleListResponse(true, [Rule("replacement")], TestFirewallConfiguration.Enabled),
            outcome == RuleReplacementOutcome.Completed ? Rule("replacement") : null, RecoveryOutcome: null, Diagnostic: null);
        return new RuleReplacementMutationResponse(firewall, reconciliation);
    }

    private sealed class TestHost
    {
        public Mock<IRuleMutationService> Mutations { get; } = new(MockBehavior.Strict);
        public Mock<IRuleMetadataMutationService> Metadata { get; } = new(MockBehavior.Strict);
        public Mock<IClientErrorMapper> Errors { get; } = new(MockBehavior.Strict);
        public RuleReplacementWorkflowService Service => new(Mutations.Object, Metadata.Object, Errors.Object);
    }
}
