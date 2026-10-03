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
        client.Setup(static c => c.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleDaemonGateway gateway = new(client.Object);

        DaemonResult<RuleListResponse> result = await gateway.GetRulesAsync();

        Assert.AreSame(expected, result.Result);
        client.VerifyAll();
    }

    [TestMethod]
    public async Task GetRulesAsync_DaemonApplicationFailureIsReturnedWithoutThrowingAsync()
    {
        UfwIpcException expected = new(409, "conflict");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ThrowsAsync(expected);
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
        client.Setup(static c => c.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>())).ThrowsAsync(expected);
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
        client.Setup(c => c.SendAsync<AddRuleRequest, RuleMutationResponse>(add, It.IsAny<CancellationToken>())).ReturnsAsync(addResponse);
        client.Setup(c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(insert, It.IsAny<CancellationToken>())).ReturnsAsync(insertResponse);
        client.Setup(c => c.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(replace, It.IsAny<CancellationToken>())).ReturnsAsync(replaceResponse);
        client.Setup(c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(reorder, It.IsAny<CancellationToken>())).ReturnsAsync(reorderResponse);
        client.Setup(c => c.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(batchDelete, It.IsAny<CancellationToken>())).ReturnsAsync(batchDeleteResponse);
        client.Setup(c => c.SendAsync<DeleteRuleRequest, RuleMutationResponse>(delete, It.IsAny<CancellationToken>())).ReturnsAsync(deleteResponse);
        RuleDaemonGateway gateway = new(client.Object);

        Assert.AreSame(addResponse, (await gateway.AddRuleAsync(add)).Result);
        Assert.AreSame(insertResponse, (await gateway.InsertRuleAsync(insert)).Result);
        Assert.AreSame(replaceResponse, (await gateway.ReplaceRuleAsync(replace)).Result);
        Assert.AreSame(reorderResponse, (await gateway.ReorderRulesAsync(reorder)).Result);
        Assert.AreSame(batchDeleteResponse, (await gateway.BatchDeleteRulesAsync(batchDelete)).Result);
        Assert.AreSame(deleteResponse, (await gateway.DeleteRuleAsync(delete)).Result);
        client.VerifyAll();
    }
}
