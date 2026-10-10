using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Authoring.Workflows;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleCreationWorkflowServiceTests
{
    [TestMethod]
    public async Task AddAsync_UsesConfirmedIdentityAndPersistsMetadataAfterFirewallMutationAsync()
    {
        using CancellationTokenSource lifetime = new();
        TestHost host = new();
        FirewallRuleSpecification rule = Specification();
        RuleMetadataChange metadata = new("note", [Guid.CreateVersion7()], null);
        RuleMutationResponse firewall = new("add", Rule("confirmed"));
        List<string> steps = [];
        host.Reconciliation.Setup(service => service.GetRequestedIdentity(It.IsAny<FirewallRuleSpecification>())).Returns("requested");
        host.Reconciliation.Setup(service => service.GetMutationIdentity(firewall, "requested")).Returns("confirmed");
        host.Mutations.Setup(service => service.AddRuleAsync(It.IsAny<FirewallRuleSpecification>(), "key", lifetime.Token))
            .Callback(() => steps.Add("firewall")).ReturnsAsync(firewall);
        host.Metadata.Setup(service => service.UpdateAsync("confirmed", metadata, lifetime.Token))
            .Callback(() => steps.Add("metadata")).ReturnsAsync(new RuleMetadataMutationResponse());

        RuleCreationAddResult result = await host.Service.AddAsync(rule, metadata, "key", lifetime.Token);

        Assert.AreSame(firewall, result.Firewall);
        Assert.AreEqual("confirmed", result.ConfirmedRuleId);
        Assert.IsNull(result.MetadataError);
        CollectionAssert.AreEqual(new[] { "firewall", "metadata" }, steps);
        host.Mutations.VerifyAll();
        host.Metadata.VerifyAll();
    }

    [TestMethod]
    public async Task AddAsync_MetadataFailureDoesNotLoseConfirmedFirewallSuccessAsync()
    {
        TestHost host = new();
        RuleMutationResponse firewall = new("add", Rule("confirmed"));
        RuleMetadataChange metadata = new("note", [], null);
        ClientError mapped = new(ClientErrorKind.Unavailable, "metadata unavailable", Retryable: true);
        host.Reconciliation.Setup(service => service.GetRequestedIdentity(It.IsAny<FirewallRuleSpecification>())).Returns("requested");
        host.Reconciliation.Setup(service => service.GetMutationIdentity(firewall, "requested")).Returns("confirmed");
        host.Mutations.Setup(service => service.AddRuleAsync(It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(firewall);
        host.Metadata.Setup(service => service.UpdateAsync("confirmed", metadata, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        host.Errors.Setup(mapper => mapper.Describe(It.IsAny<HttpRequestException>())).Returns(mapped);

        RuleCreationAddResult result = await host.Service.AddAsync(Specification(), metadata, "key");

        Assert.AreSame(firewall, result.Firewall);
        Assert.AreEqual("confirmed", result.ConfirmedRuleId);
        Assert.AreSame(mapped, result.MetadataError);
    }

    [TestMethod]
    public async Task AddAsync_FirewallFailureNeverAttemptsMetadataAsync()
    {
        TestHost host = new();
        host.Reconciliation.Setup(service => service.GetRequestedIdentity(It.IsAny<FirewallRuleSpecification>())).Returns("requested");
        host.Mutations.Setup(service => service.AddRuleAsync(It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("mutation failed"));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => host.Service.AddAsync(Specification(), new RuleMetadataChange("note", [], null), "key"));

        host.Metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task InsertAsync_NonCompletedResultDoesNotPersistMetadataAsync()
    {
        TestHost host = new();
        RuleSnapshot snapshot = Snapshot();
        OrderedRuleInsertionNavigationContext context = Context(snapshot);
        RuleInsertionResponse firewall = new(RuleInsertionOutcome.StaleBaseline, RuleSnapshotFactory.ToFirewallResponse(snapshot), null, "stale");
        host.Mutations.Setup(service => service.InsertRuleAsync(It.IsAny<RuleListResponse>(), 0, RuleInsertionPlacement.Before,
            It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(firewall);

        RuleCreationInsertionResult result = await host.Service.InsertAsync(snapshot, context, Specification(), new RuleMetadataChange("note", [], null), "key");

        Assert.AreSame(firewall, result.Firewall);
        Assert.IsNull(result.MetadataError);
        host.Metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task InsertAsync_UsesConfirmedInsertedRuleIdAndPreservesSnapshotAssessmentAsync()
    {
        TestHost host = new();
        RuleSnapshot snapshot = Snapshot();
        OrderedRuleInsertionNavigationContext context = Context(snapshot);
        RuleInsertionResponse firewall = new(RuleInsertionOutcome.Completed, RuleSnapshotFactory.ToFirewallResponse(snapshot), Rule("inserted"), null);
        RuleMetadataChange metadata = new("note", [], null);
        host.Mutations.Setup(service => service.InsertRuleAsync(
            It.Is<RuleListResponse>(baseline => baseline.Assessment == snapshot.Assessment), 0, RuleInsertionPlacement.Before,
            It.IsAny<FirewallRuleSpecification>(), "key", It.IsAny<CancellationToken>())).ReturnsAsync(firewall);
        host.Metadata.Setup(service => service.UpdateAsync("inserted", metadata, It.IsAny<CancellationToken>())).ReturnsAsync(new RuleMetadataMutationResponse());

        RuleCreationInsertionResult result = await host.Service.InsertAsync(snapshot, context, Specification(), metadata, "key");

        Assert.AreSame(firewall, result.Firewall);
        Assert.IsNull(result.MetadataError);
        host.Metadata.VerifyAll();
    }

    [TestMethod]
    public async Task AddAsync_CancellationDuringMetadataSaveIsNotConvertedIntoSuccessAsync()
    {
        using CancellationTokenSource lifetime = new();
        TestHost host = new();
        RuleMutationResponse firewall = new("add", Rule("confirmed"));
        RuleMetadataChange metadata = new("note", [], null);
        host.Reconciliation.Setup(service => service.GetRequestedIdentity(It.IsAny<FirewallRuleSpecification>())).Returns("requested");
        host.Reconciliation.Setup(service => service.GetMutationIdentity(firewall, "requested")).Returns("confirmed");
        host.Mutations.Setup(service => service.AddRuleAsync(It.IsAny<FirewallRuleSpecification>(), "key", lifetime.Token)).ReturnsAsync(firewall);
        host.Metadata.Setup(service => service.UpdateAsync("confirmed", metadata, lifetime.Token)).Returns(async () =>
        {
            await lifetime.CancelAsync();
            lifetime.Token.ThrowIfCancellationRequested();
            return new RuleMetadataMutationResponse();
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => host.Service.AddAsync(Specification(), metadata, "key", lifetime.Token));
    }

    private static FirewallRuleSpecification Specification() => new() { Action = FirewallAction.Allow, AddressFamily = FirewallAddressFamily.IPv4 };

    private static ListedFirewallRule Rule(string ruleId) => new() { RuleId = ruleId, Parsed = true, Rule = Specification() };

    private static RuleSnapshot Snapshot() => new(true, [Rule("anchor")], TestFirewallConfiguration.Enabled);

    private static OrderedRuleInsertionNavigationContext Context(RuleSnapshot snapshot) => new("baseline", 0, 1, RuleInsertionPlacement.Before,
        FirewallAddressFamily.IPv4, snapshot.Rules[0]);

    private sealed class TestHost
    {
        public Mock<IRuleMutationService> Mutations { get; } = new(MockBehavior.Strict);
        public Mock<IRuleMetadataMutationService> Metadata { get; } = new(MockBehavior.Strict);
        public Mock<IRuleMutationReconciliationService> Reconciliation { get; } = new(MockBehavior.Strict);
        public Mock<IClientErrorMapper> Errors { get; } = new(MockBehavior.Strict);
        public RuleCreationWorkflowService Service => new(Mutations.Object, Metadata.Object, Reconciliation.Object, Errors.Object);
    }
}
