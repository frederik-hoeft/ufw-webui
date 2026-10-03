using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
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
        RuleDaemonGateway gateway = new(client.Object);

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
        RuleDaemonGateway gateway = new(client.Object);

        DaemonResult<RuleListResponse> result = await gateway.GetRulesAsync();

        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(expected, result.Error);
        client.VerifyAll();
    }


    [TestMethod]
    public async Task GetRulesAsync_InvalidIpcResponseIsClassifiedAtGatewayBoundaryAsync()
    {
        InvalidDataException expected = new("unsupported daemon payload");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.TrySendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ThrowsAsync(expected);
        RuleDaemonGateway gateway = new(client.Object);

        DaemonInvalidResponseException actual = await Assert.ThrowsExactlyAsync<DaemonInvalidResponseException>(() => gateway.GetRulesAsync());

        Assert.AreSame(expected, actual.InnerException);
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
        RuleReplacementResponse replaceResponse = new(RuleReplacementOutcome.Completed, null, null, null, null);
        RuleReorderResponse reorderResponse = new(RuleReorderOutcome.Completed, null, [], [], [], null);
        RuleBatchDeleteResponse batchDeleteResponse = new(RuleBatchDeleteOutcome.Completed, null, [], [], null);
        RuleMutationResponse deleteResponse = new("rules.delete", null!);
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.TrySendAsync<AddRuleRequest, RuleMutationResponse>(add, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleMutationResponse>.Success(addResponse));
        client.Setup(c => c.TrySendAsync<InsertRuleRequest, RuleInsertionResponse>(insert, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleInsertionResponse>.Success(insertResponse));
        client.Setup(c => c.TrySendAsync<ReplaceRuleRequest, RuleReplacementResponse>(replace, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReplacementResponse>.Success(replaceResponse));
        client.Setup(c => c.TrySendAsync<ReorderRulesRequest, RuleReorderResponse>(reorder, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleReorderResponse>.Success(reorderResponse));
        client.Setup(c => c.TrySendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(batchDelete, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleBatchDeleteResponse>.Success(batchDeleteResponse));
        client.Setup(c => c.TrySendAsync<DeleteRuleRequest, RuleMutationResponse>(delete, It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<RuleMutationResponse>.Success(deleteResponse));
        RuleDaemonGateway gateway = new(client.Object);

        DaemonResult<RuleMutationResponse> addResult = await gateway.AddRuleAsync(add);
        DaemonResult<RuleInsertionResponse> insertResult = await gateway.InsertRuleAsync(insert);
        DaemonResult<RuleReplacementResponse> replaceResult = await gateway.ReplaceRuleAsync(replace);
        DaemonResult<RuleReorderResponse> reorderResult = await gateway.ReorderRulesAsync(reorder);
        DaemonResult<RuleBatchDeleteResponse> batchDeleteResult = await gateway.BatchDeleteRulesAsync(batchDelete);
        DaemonResult<RuleMutationResponse> deleteResult = await gateway.DeleteRuleAsync(delete);

        Assert.AreSame(addResponse, addResult.Result);
        Assert.AreSame(insertResponse, insertResult.Result);
        Assert.AreSame(replaceResponse, replaceResult.Result);
        Assert.AreSame(reorderResponse, reorderResult.Result);
        Assert.AreSame(batchDeleteResponse, batchDeleteResult.Result);
        Assert.AreSame(deleteResponse, deleteResult.Result);
        client.VerifyAll();
    }
}
