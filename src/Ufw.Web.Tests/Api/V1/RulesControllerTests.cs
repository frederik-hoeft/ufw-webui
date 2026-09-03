using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Web.Api.V1.Controllers;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RulesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TestGetRulesAsync_ReturnsDaemonSnapshotAsync()
    {
        Mock<IUfwClient> client = new();
        RuleListResponse expected = new(
            Active: true,
            [
                new ListedFirewallRule
                {
                    RuleId = "sha256:abc",
                    DisplayNumber = 1,
                    Parsed = true,
                    RawLine = "[ 1] 22/tcp ALLOW IN Anywhere",
                    Rule = new FirewallRuleSpecification
                    {
                        Action = FirewallAction.Allow,
                        Direction = FirewallDirection.In,
                        Protocol = FirewallProtocol.Tcp,
                        DestinationPorts = "22",
                    }
                }
            ]);
        client
            .Setup(static c => c.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        RulesController controller = CreateController(client.Object);
        ActionResult<RuleListResponse> result = await controller.GetRulesAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task TestAddRuleAsync_ForwardsSignedEnvelopeAsync()
    {
        Mock<IUfwClient> client = new();
        AddRuleRequest request = CreateSignedAdd();
        RuleMutationResponse expected = new(IntentOperations.ADD_RULE, null!);
        client.Setup(static c => c.SendAsync<AddRuleRequest, RuleMutationResponse>(It.IsAny<AddRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        RulesController controller = CreateController(client.Object);
        ActionResult<RuleMutationResponse> result = await controller.AddRuleAsync(request, TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
        client.Verify(
            c => c.SendAsync<AddRuleRequest, RuleMutationResponse>(
                It.Is<AddRuleRequest>(sent => sent.DeploymentId == request.DeploymentId
                    && sent.Nonce == request.Nonce
                    && sent.Signature == request.Signature),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TestAddRuleAsync_RejectsWrongOperationAsync()
    {
        Mock<IUfwClient> client = new();
        AddRuleRequest request = CreateSignedAdd() with { Operation = IntentOperations.DELETE_RULE };
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleMutationResponse> result = await controller.AddRuleAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        client.Verify(
            static c => c.SendAsync<AddRuleRequest, RuleMutationResponse>(It.IsAny<AddRuleRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestDeleteRuleAsync_MapsDaemonConflictAsync()
    {
        Mock<IUfwClient> client = new();
        client
            .Setup(static c => c.SendAsync<DeleteRuleRequest, RuleMutationResponse>(It.IsAny<DeleteRuleRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status409Conflict, "A semantically identical rule already exists."));

        RulesController controller = CreateController(client.Object);
        ActionResult<RuleMutationResponse> result = await controller.DeleteRuleAsync(CreateSignedDelete(), TestContext.CancellationToken);

        ObjectResult problem = (ObjectResult)result.Result!;
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    private static RulesController CreateController(IUfwClient client)
    {
        RulesController controller = new(client)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static AddRuleRequest CreateSignedAdd() => new()
    {
        Version = 1,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce",
        Operation = IntentOperations.ADD_RULE,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { rule = new { action = "allow" } }),
        Signature = "sig",
    };

    private static DeleteRuleRequest CreateSignedDelete() => new()
    {
        Version = 1,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULE,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { ruleId = "sha256:x", rule = new { action = "allow" } }),
        Signature = "sig",
    };
}
