using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using Ufw.Ipc.Client;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Services.Rules;

[TestClass]
public sealed class RuleDaemonGatewayTests
{
    [TestMethod]
    public async Task GetRulesAsync_UsesExpectedDaemonEndpointAsync()
    {
        RuleListResponse expected = new(Active: true, [], TestFirewallConfiguration.Enabled);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.TrySendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleListResponse>.Success(expected));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleListResponse> result = await gateway.GetRulesAsync();

        Assert.AreSame(expected, result.Result);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task GetRulesAsync_DaemonApplicationFailureIsReturnedWithoutThrowingAsync()
    {
        UfwIpcError expected = new(409, "conflict");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.TrySendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleListResponse>.Failure(expected));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleListResponse> result = await gateway.GetRulesAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task GetRulesAsync_InvalidIpcResponseIsClassifiedAtGatewayBoundaryAsync()
    {
        UfwIpcInvalidResponseException expected = new("unsupported daemon payload");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.TrySendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ThrowsAsync(expected);
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonInvalidResponseException actual = await Assert.ThrowsExactlyAsync<DaemonInvalidResponseException>(() => gateway.GetRulesAsync());

        Assert.AreSame(expected, actual.InnerException);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task ReplaceRuleAsync_CompletedReplacementProducesReconciliationFactsAsync()
    {
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        ReplaceRuleRequest request = CreateReplaceRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, originalRuleId, replacementRuleId);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(response));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleReplacementExecutionResult> result = await gateway.ReplaceRuleAsync(request);

        RuleReplacementExecutionResult replacement = result.Result;
        Assert.AreSame(response, replacement.Firewall);
        RuleReplacementReconciliationReady ready = Assert.IsInstanceOfType<RuleReplacementReconciliationReady>(replacement.Reconciliation);
        Assert.AreEqual(originalRuleId, ready.Facts.OriginalRuleId);
        Assert.AreEqual(replacementRuleId, ready.Facts.ReplacementRuleId);
        Assert.IsTrue(ready.Facts.OriginalRuleStillLive);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task ReplaceRuleAsync_NonCompletedReplacementDoesNotInterpretSignedPayloadAsync()
    {
        ReplaceRuleRequest request = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        RuleReplacementResponse response = new(RuleReplacementOutcome.PreconditionFailed, null, null, null, "precondition");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(response));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleReplacementExecutionResult> result = await gateway.ReplaceRuleAsync(request);

        Assert.AreSame(response, result.Result.Firewall);
        Assert.IsInstanceOfType<RuleReplacementReconciliationNotRequired>(result.Result.Reconciliation);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task ReplaceRuleAsync_InvalidSignedPayloadMarksReconciliationFailedWithoutDiscardingFirewallResultAsync()
    {
        FirewallRuleSpecification replacementRule = Rule("443");
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        ReplaceRuleRequest request = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = IntentOperations.REPLACE_RULE, Payload = default, Signature = "signature" };
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, replacementRuleId);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(response));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleReplacementExecutionResult> result = await gateway.ReplaceRuleAsync(request);

        Assert.AreSame(response, result.Result.Firewall);
        Assert.IsInstanceOfType<RuleReplacementReconciliationPreparationFailed>(result.Result.Reconciliation);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task ReplaceRuleAsync_MismatchedReplacementIdentityMarksReconciliationFailedAsync()
    {
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string mismatchedReplacementId = RuleIdentity.Compute(Rule("8443"));
        ReplaceRuleRequest request = CreateReplaceRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(mismatchedReplacementId, replacementRule, mismatchedReplacementId);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(response));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleReplacementExecutionResult> result = await gateway.ReplaceRuleAsync(request);

        Assert.IsInstanceOfType<RuleReplacementReconciliationPreparationFailed>(result.Result.Reconciliation);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task ReplaceRuleAsync_FinalSnapshotMustConfirmReplacementIdentityAsync()
    {
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        ReplaceRuleRequest request = CreateReplaceRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, originalRuleId);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(response));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleReplacementExecutionResult> result = await gateway.ReplaceRuleAsync(request);

        Assert.IsInstanceOfType<RuleReplacementReconciliationPreparationFailed>(result.Result.Reconciliation);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task SignedMutationMethods_ForwardRequestOwnedProtocolMetadataAsync()
    {
        AddRuleRequest add = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        InsertRuleRequest insert = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        ReplaceRuleRequest replace = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        ReorderRulesRequest reorder = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        BatchDeleteRulesRequest batchDelete = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        DeleteRuleRequest delete = new() { DeploymentId = "deployment", KeyId = "key", Nonce = "nonce", Operation = "operation", Payload = default, Signature = "signature" };
        RuleMutationResponse addResponse = new("rules.add", null!);
        RuleInsertionResponse insertResponse = new(RuleInsertionOutcome.Completed, null, null, null);
        RuleReplacementResponse replaceResponse = new(RuleReplacementOutcome.PreconditionFailed, null, null, null, null);
        RuleReorderResponse reorderResponse = new(RuleReorderOutcome.Completed, null, [], [], [], null);
        RuleBatchDeleteResponse batchDeleteResponse = new(RuleBatchDeleteOutcome.Completed, null, [], [], null);
        RuleMutationResponse deleteResponse = new("rules.delete", null!);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<AddRuleRequest, RuleMutationResponse>(add, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleMutationResponse>.Success(addResponse));
        client.Setup(c => c.TrySendAsync<InsertRuleRequest, RuleInsertionResponse>(insert, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleInsertionResponse>.Success(insertResponse));
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(replace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(replaceResponse));
        client.Setup(c => c.TrySendAsync<ReorderRulesRequest, RuleReorderResponse>(reorder, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReorderResponse>.Success(reorderResponse));
        client.Setup(c => c.TrySendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(batchDelete, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UfwIpcResult<RuleBatchDeleteResponse>.Success(batchDeleteResponse));
        client.Setup(c => c.TrySendAsync<DeleteRuleRequest, RuleMutationResponse>(delete, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleMutationResponse>.Success(deleteResponse));
        RuleDaemonGateway gateway = CreateGateway(client.Object);

        DaemonResult<RuleMutationResponse> addResult = await gateway.AddRuleAsync(add);
        DaemonResult<RuleInsertionResponse> insertResult = await gateway.InsertRuleAsync(insert);
        DaemonResult<RuleReplacementExecutionResult> replaceResult = await gateway.ReplaceRuleAsync(replace);
        DaemonResult<RuleReorderResponse> reorderResult = await gateway.ReorderRulesAsync(reorder);
        DaemonResult<RuleBatchDeleteResponse> batchDeleteResult = await gateway.BatchDeleteRulesAsync(batchDelete);
        DaemonResult<RuleMutationResponse> deleteResult = await gateway.DeleteRuleAsync(delete);

        Assert.AreSame(addResponse, addResult.Result);
        Assert.AreSame(insertResponse, insertResult.Result);
        Assert.AreSame(replaceResponse, replaceResult.Result.Firewall);
        Assert.IsInstanceOfType<RuleReplacementReconciliationNotRequired>(replaceResult.Result.Reconciliation);
        Assert.AreSame(reorderResponse, reorderResult.Result);
        Assert.AreSame(batchDeleteResponse, batchDeleteResult.Result);
        Assert.AreSame(deleteResponse, deleteResult.Result);
        client.VerifyAll();
    }

    private static RuleDaemonGateway CreateGateway(IUfwClient client) => new(client, NullLogger<RuleDaemonGateway>.Instance);

    private static FirewallRuleSpecification Rule(string destinationPort) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = destinationPort,
    };

    private static ReplaceRuleRequest CreateReplaceRequest(string originalRuleId, FirewallRuleSpecification replacementRule)
    {
        ReplaceRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            TargetOccurrenceId = 0,
            OriginalRuleId = originalRuleId,
            ReplacementRule = replacementRule,
        };
        return new ReplaceRuleRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = "deployment",
            KeyId = "sha256:key",
            IssuedAtUnix = 1,
            Nonce = "nonce",
            Operation = IntentOperations.REPLACE_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.ReplaceRulePayload),
            Signature = "signature",
        };
    }

    private static RuleReplacementResponse CompletedReplacement(string replacementRuleId, FirewallRuleSpecification replacementRule, params string[] finalRuleIds)
    {
        ListedFirewallRule replacement = new()
        {
            RuleId = replacementRuleId,
            DisplayNumber = Array.IndexOf(finalRuleIds, replacementRuleId) + 1,
            Parsed = true,
            RawLine = replacementRuleId,
            Rule = replacementRule,
        };
        ListedFirewallRule[] finalRules = [.. finalRuleIds.Select((ruleId, index) => string.Equals(ruleId, replacementRuleId, StringComparison.Ordinal)
            ? new ListedFirewallRule
            {
                RuleId = replacement.RuleId,
                DisplayNumber = index + 1,
                Parsed = replacement.Parsed,
                RawLine = replacement.RawLine,
                Rule = replacement.Rule,
            }
            : Listed(ruleId, index + 1))];
        return new RuleReplacementResponse(
            RuleReplacementOutcome.Completed,
            new RuleListResponse(true, finalRules, TestFirewallConfiguration.Enabled),
            replacement,
            RecoveryOutcome: null,
            Diagnostic: null);
    }

    private static ListedFirewallRule Listed(string ruleId, int displayNumber) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
    };
}
