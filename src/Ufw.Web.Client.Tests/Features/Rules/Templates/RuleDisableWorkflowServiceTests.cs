using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Rules.Intent;
using Ufw.Web.Client.Api.Intent;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Tests.Features.Rules.Templates;

[TestClass]
public sealed class RuleDisableWorkflowServiceTests
{
    [TestMethod]
    public async Task DisableAsync_PersistsExactReusableSnapshotBeforeOrdinaryDeleteAsync()
    {
        TestHost host = new();
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleRowProjection row = Row(canMutate: true, new RuleMetadata(
            Guid.CreateVersion7(),
            "  keep this  ",
            [new RuleTag(tagId, "prod", "#112233")],
            new RuleGroupMembership(groupId, "edge", null)));
        RuleTemplateDefinition? captured = null;
        host.Templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<RuleTemplateDefinition, CancellationToken>((definition, _) => captured = definition)
            .ReturnsAsync([]);
        RuleMutationResponse deleteResponse = new(IntentOperations.DELETE_RULE, row.Rule);
        host.Mutations.Setup(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>())).ReturnsAsync(deleteResponse);

        RuleDisableWorkflowResult result = await host.Service.DisableAsync(row, "  web access  ", "  temporarily disabled  ", "private-key");

        Assert.IsTrue(result.Completed);
        Assert.IsTrue(result.TemplatePersistenceConfirmed);
        Assert.AreEqual("web access", result.TemplateName);
        Assert.AreSame(deleteResponse, result.DeleteResponse);
        Assert.IsNull(result.Error);
        Assert.IsNotNull(captured);
        Assert.AreEqual("web access", captured.Name);
        Assert.AreEqual("temporarily disabled", captured.Description);
        Assert.AreEqual("keep this", captured.Notes);
        CollectionAssert.AreEqual(new[] { tagId }, captured.TagIds.ToArray());
        Assert.AreEqual(groupId, captured.GroupId);
        Assert.AreNotSame(row.Rule.Rule, captured.Rule);
        Assert.AreEqual(row.Rule.Rule!.DestinationPorts, captured.Rule.DestinationPorts);
        host.Templates.Verify(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
        host.Mutations.Verify(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>()), Times.Once);
        host.Mutations.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisableAsync_WithoutExplicitNameGeneratesReadableTemplateNameAsync()
    {
        TestHost host = new();
        RuleRowProjection row = Row(canMutate: true);
        RuleTemplateDefinition? saved = null;
        host.Templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>()))
            .Callback<RuleTemplateDefinition, CancellationToken>((definition, _) => saved = definition)
            .ReturnsAsync([]);
        host.Mutations.Setup(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMutationResponse(IntentOperations.DELETE_RULE, row.Rule));

        RuleDisableWorkflowResult result = await host.Service.DisableAsync(row, null, null, "private-key");

        Assert.IsTrue(result.Completed);
        Assert.IsNotNull(saved);
        Assert.AreEqual("allow in from 10.0.0.0/8 to 192.0.2.10 port 443 proto tcp", saved.Name);
        Assert.AreEqual(saved.Name, result.TemplateName);
    }

    [TestMethod]
    public async Task DisableAsync_UsesExistingSignedDeleteMutationPipelineAsync()
    {
        Mock<IRuleTemplateCatalogService> templates = new(MockBehavior.Strict);
        Mock<IIntentContextApiClient> intentContext = new(MockBehavior.Strict);
        Mock<IIntentSigningService> signer = new(MockBehavior.Strict);
        Mock<IRuleApiClient> ruleApi = new(MockBehavior.Strict);
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        RuleRowProjection row = Row(canMutate: true);
        DeleteRuleIntentRequest signedRequest = new()
        {
            DeploymentId = "deployment",
            KeyId = "key-id",
            Nonce = "nonce",
            Operation = IntentOperations.DELETE_RULE,
            Payload = default,
            Signature = "signature",
        };
        RuleMutationResponse deleteResponse = new(IntentOperations.DELETE_RULE, row.Rule);
        templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        intentContext.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "deployment"));
        signer.Setup(service => service.CreateDeleteRuleRequestAsync(
                "deployment",
                row.Rule.RuleId!,
                row.Rule.Rule!,
                "private-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(signedRequest);
        ruleApi.Setup(client => client.DeleteRuleAsync(signedRequest, It.IsAny<CancellationToken>())).ReturnsAsync(deleteResponse);
        RuleMutationService mutationService = new(ruleApi.Object, intentContext.Object, signer.Object);
        RuleDisableWorkflowService service = new(templates.Object, new RuleTemplateAuthoringService(new RuleDraftFactory()), mutationService, errors.Object, new RuleTemplateNameGenerator(new UfwRuleCommandRenderer()));

        RuleDisableWorkflowResult result = await service.DisableAsync(row, "web", null, "private-key");

        Assert.IsTrue(result.Completed);
        Assert.AreEqual(IntentOperations.DELETE_RULE, result.DeleteResponse?.Operation);
        signer.VerifyAll();
        ruleApi.VerifyAll();
        errors.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisableAsync_TemplatePersistenceFailureNeverInvokesFirewallDeleteAsync()
    {
        TestHost host = new();
        RuleRowProjection row = Row(canMutate: true);
        InvalidOperationException failure = new("template unavailable");
        ClientError expected = new(ClientErrorKind.Unavailable, "template API unavailable", Retryable: true);
        host.Templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Errors.Setup(mapper => mapper.Describe(failure)).Returns(expected);

        RuleDisableWorkflowResult result = await host.Service.DisableAsync(row, "web", null, "private-key");

        Assert.AreEqual(RuleDisableWorkflowOutcome.TemplatePersistenceNotConfirmed, result.Outcome);
        Assert.IsFalse(result.TemplatePersistenceConfirmed);
        Assert.IsFalse(result.Completed);
        Assert.AreSame(expected, result.Error);
        Assert.IsNull(result.DeleteResponse);
        host.Mutations.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task DisableAsync_DeleteFailureRetainsConfirmedTemplateAndReturnsTypedErrorAsync()
    {
        TestHost host = new();
        RuleRowProjection row = Row(canMutate: true);
        InvalidOperationException failure = new("delete rejected");
        ClientError expected = new(ClientErrorKind.Conflict, "firewall state changed", Retryable: false);
        host.Templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        host.Mutations.Setup(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Errors.Setup(mapper => mapper.Describe(failure)).Returns(expected);

        RuleDisableWorkflowResult result = await host.Service.DisableAsync(row, "web", null, "private-key");

        Assert.AreEqual(RuleDisableWorkflowOutcome.FirewallDeleteNotConfirmed, result.Outcome);
        Assert.IsTrue(result.TemplatePersistenceConfirmed);
        Assert.IsFalse(result.Completed);
        Assert.AreSame(expected, result.Error);
        Assert.IsNull(result.DeleteResponse);
        host.Templates.Verify(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>()), Times.Once);
        host.Mutations.Verify(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task DisableAsync_TransportFailureAfterTemplatePersistenceIsReportedAsUncertainFirewallOutcomeAsync()
    {
        TestHost host = new();
        RuleRowProjection row = Row(canMutate: true);
        HttpRequestException failure = new("connection reset");
        ClientError expected = new(ClientErrorKind.Unavailable, "API unavailable", Retryable: true);
        host.Templates.Setup(catalog => catalog.CreateAsync(It.IsAny<RuleTemplateDefinition>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        host.Mutations.Setup(service => service.DeleteRuleAsync(row.Rule, "private-key", It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        host.Errors.Setup(mapper => mapper.Describe(failure)).Returns(expected);

        RuleDisableWorkflowResult result = await host.Service.DisableAsync(row, "web", null, "private-key");

        Assert.AreEqual(RuleDisableWorkflowOutcome.FirewallDeleteNotConfirmed, result.Outcome);
        Assert.IsTrue(result.TemplatePersistenceConfirmed);
        Assert.AreEqual(ClientErrorKind.Unavailable, result.Error?.Kind);
    }

    [TestMethod]
    public async Task DisableAsync_RejectsUnsafeSourceBeforeTemplateOrFirewallMutationAsync()
    {
        TestHost host = new();
        RuleRowProjection duplicateSemanticRow = Row(canMutate: false);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.Service.DisableAsync(duplicateSemanticRow, "web", null, "private-key"));

        host.Templates.VerifyNoOtherCalls();
        host.Mutations.VerifyNoOtherCalls();
        host.Errors.VerifyNoOtherCalls();
    }

    private static RuleRowProjection Row(bool canMutate, RuleMetadata? metadata = null)
    {
        FirewallRuleSpecification specification = new()
        {
            AddressFamily = FirewallAddressFamily.IPv4,
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "10.0.0.0/8",
            Destination = "192.0.2.10",
            DestinationPorts = "443",
        };
        ListedFirewallRule listed = new()
        {
            Parsed = true,
            RuleId = RuleIdentity.Compute(specification),
            Rule = specification,
        };
        return new RuleRowProjection(listed, FirewallAddressFamily.IPv4, 0, 1, 1, CanOrder: true, CanMutate: canMutate, PositionChange: null, Metadata: metadata, CanonicalCommand: "ufw allow 443/tcp");
    }

    private sealed class TestHost
    {
        public Mock<IRuleTemplateCatalogService> Templates { get; } = new(MockBehavior.Strict);
        public Mock<IRuleMutationService> Mutations { get; } = new(MockBehavior.Strict);
        public Mock<IClientErrorMapper> Errors { get; } = new(MockBehavior.Strict);
        public RuleDisableWorkflowService Service { get; }

        public TestHost()
        {
            Service = new RuleDisableWorkflowService(
                Templates.Object, new RuleTemplateAuthoringService(new RuleDraftFactory()), Mutations.Object, Errors.Object, new RuleTemplateNameGenerator(new UfwRuleCommandRenderer()));
        }
    }
}
