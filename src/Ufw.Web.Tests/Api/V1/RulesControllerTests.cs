using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RulesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TestGetRulesAsync_ReturnsEnrichedInventoryAsync()
    {
        Mock<IUfwClient> client = new();
        Mock<IRuleInventoryService> inventory = new();
        RuleListResponse firewall = new(
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
                    },
                },
            ],
            TestFirewallConfiguration.Enabled);
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        RuleInventoryResponse expected = new(
            firewall,
            [new RuleMetadataItem(metadataId, "sha256:abc", "ssh", [new RuleTagItem(tagId, "prod", "#336699")])]);
        inventory.Setup(service => service.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        RulesController controller = CreateController(client.Object, inventory.Object);
        ActionResult<RuleInventoryResponse> result = await controller.GetRulesAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task TestUpdateMetadataAsync_ReturnsServiceResultAsync()
    {
        Mock<IUfwClient> client = new();
        Mock<IRuleMetadataService> metadata = new();
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        UpdateRuleMetadataRequest request = new() { TagIds = [tagId] };
        RuleMetadataMutationResponse expected = new(
            new RuleMetadataItem(metadataId, "sha256:abc", null, [new RuleTagItem(tagId, "prod", "#336699")]));
        metadata.Setup(service => service.UpdateAsync("sha256:abc", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.Success, expected));

        RulesController controller = CreateController(client.Object, metadata: metadata.Object);
        ActionResult<RuleMetadataMutationResponse> result = await controller.UpdateMetadataAsync(
            "sha256:abc", request, TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task TestUpdateMetadataAsync_MapsMissingAndInvalidMetadataAsync()
    {
        Mock<IUfwClient> client = new();
        Mock<IRuleMetadataService> metadata = new();
        UpdateRuleMetadataRequest missingRequest = new();
        UpdateRuleMetadataRequest invalidRequest = new();
        UpdateRuleMetadataRequest missingTagRequest = new();
        metadata.Setup(service => service.UpdateAsync("missing", missingRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.RuleNotFound));
        metadata.Setup(service => service.UpdateAsync("invalid", invalidRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.InvalidMetadata));
        metadata.Setup(service => service.UpdateAsync("missing-tag", missingTagRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.TagNotFound));
        RulesController controller = CreateController(client.Object, metadata: metadata.Object);

        ActionResult<RuleMetadataMutationResponse> missing = await controller.UpdateMetadataAsync(
            "missing", missingRequest, TestContext.CancellationToken);
        ActionResult<RuleMetadataMutationResponse> invalid = await controller.UpdateMetadataAsync(
            "invalid", invalidRequest, TestContext.CancellationToken);
        ActionResult<RuleMetadataMutationResponse> missingTag = await controller.UpdateMetadataAsync(
            "missing-tag", missingTagRequest, TestContext.CancellationToken);

        Assert.IsInstanceOfType<NotFoundResult>(missing.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(invalid.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(missingTag.Result);
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
    public void InsertRuleAsync_UsesDedicatedInsertSubresource()
    {
        System.Reflection.MethodInfo? method = typeof(RulesController).GetMethod(nameof(RulesController.InsertRuleAsync));
        Assert.IsNotNull(method);
        object[] attributes = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false);
        Assert.HasCount(1, attributes);
        HttpPostAttribute attribute = Assert.IsInstanceOfType<HttpPostAttribute>(attributes[0]);
        Assert.AreEqual("insert", attribute.Template);
    }

    [TestMethod]
    public async Task TestInsertRuleAsync_ForwardsSignedEnvelopeAsync()
    {
        Mock<IUfwClient> client = new();
        InsertRuleRequest request = CreateSignedInsert();
        RuleInsertionResponse expected = CreateInsertionResponse(RuleInsertionOutcome.Completed);
        client.Setup(static c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
                It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        client.Verify(c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
            It.Is<InsertRuleRequest>(sent => sent.DeploymentId == request.DeploymentId
                && sent.Nonce == request.Nonce
                && sent.Operation == request.Operation
                && sent.Payload.GetRawText() == request.Payload.GetRawText()
                && sent.Signature == request.Signature),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(RuleInsertionOutcome.StaleBaseline, StatusCodes.Status409Conflict)]
    [DataRow(RuleInsertionOutcome.PreconditionFailed, StatusCodes.Status422UnprocessableEntity)]
    [DataRow(RuleInsertionOutcome.StateUncertain, StatusCodes.Status503ServiceUnavailable)]
    public async Task TestInsertRuleAsync_PreservesStructuredNonSuccessReportAsync(RuleInsertionOutcome outcome, int expectedStatusCode)
    {
        Mock<IUfwClient> client = new();
        RuleInsertionResponse expected = CreateInsertionResponse(outcome);
        client.Setup(static c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
                It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(
            CreateSignedInsert(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreSame(expected, response.Value);
    }

    [TestMethod]
    public async Task TestInsertRuleAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IUfwClient> client = new();
        InsertRuleRequest request = CreateSignedInsert() with { Operation = IntentOperations.ADD_RULE };
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        client.Verify(static c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
            It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task TestInsertRuleAsync_MapsDaemonReplayConflictAsProblemDetailsAsync()
    {
        Mock<IUfwClient> client = new();
        client.Setup(static c => c.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
                It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status409Conflict, "Intent nonce has already been used."));
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(
            CreateSignedInsert(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.IsInstanceOfType<ProblemDetails>(response.Value);
    }

    [TestMethod]
    public void ReorderRulesAsync_UsesDedicatedOrderSubresource()
    {
        System.Reflection.MethodInfo? method = typeof(RulesController).GetMethod(nameof(RulesController.ReorderRulesAsync));
        Assert.IsNotNull(method);
        object[] attributes = method.GetCustomAttributes(typeof(HttpPutAttribute), inherit: false);
        Assert.HasCount(1, attributes);
        HttpPutAttribute attribute = Assert.IsInstanceOfType<HttpPutAttribute>(attributes[0]);
        Assert.AreEqual("order", attribute.Template);
    }

    [TestMethod]
    public async Task TestReorderRulesAsync_ForwardsSignedEnvelopeAsync()
    {
        Mock<IUfwClient> client = new();
        ReorderRulesRequest request = CreateSignedReorder();
        RuleReorderResponse expected = CreateReorderResponse(RuleReorderOutcome.Completed);
        client
            .Setup(static c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                It.IsAny<ReorderRulesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        RulesController controller = CreateController(client.Object);
        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        client.Verify(
            c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                It.Is<ReorderRulesRequest>(sent => sent.DeploymentId == request.DeploymentId
                    && sent.Nonce == request.Nonce
                    && sent.Operation == request.Operation
                    && sent.Payload.GetRawText() == request.Payload.GetRawText()
                    && sent.Signature == request.Signature),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    [DataRow(RuleReorderOutcome.StaleBaseline, StatusCodes.Status409Conflict)]
    [DataRow(RuleReorderOutcome.PreconditionFailed, StatusCodes.Status422UnprocessableEntity)]
    [DataRow(RuleReorderOutcome.PartiallyCompleted, StatusCodes.Status409Conflict)]
    [DataRow(RuleReorderOutcome.RecoveryFailed, StatusCodes.Status500InternalServerError)]
    [DataRow(RuleReorderOutcome.StateUncertain, StatusCodes.Status503ServiceUnavailable)]
    public async Task TestReorderRulesAsync_PreservesStructuredNonSuccessReportAsync(RuleReorderOutcome outcome, int expectedStatusCode)
    {
        Mock<IUfwClient> client = new();
        RuleReorderResponse expected = CreateReorderResponse(outcome);
        client
            .Setup(static c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                It.IsAny<ReorderRulesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(
            CreateSignedReorder(),
            TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreSame(expected, response.Value);
    }

    [TestMethod]
    public async Task TestReorderRulesAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IUfwClient> client = new();
        ReorderRulesRequest request = CreateSignedReorder() with { Operation = IntentOperations.ADD_RULE };
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        client.Verify(
            static c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                It.IsAny<ReorderRulesRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestReorderRulesAsync_MapsDaemonReplayConflictAsProblemDetailsAsync()
    {
        Mock<IUfwClient> client = new();
        client
            .Setup(static c => c.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                It.IsAny<ReorderRulesRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status409Conflict, "Intent nonce has already been used."));
        RulesController controller = CreateController(client.Object);

        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(
            CreateSignedReorder(),
            TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.IsInstanceOfType<ProblemDetails>(response.Value);
    }

    [TestMethod]
    public async Task TestDeleteRuleAsync_RemovesMetadataForAuthoritativeDeletedIdentityAsync()
    {
        Mock<IUfwClient> client = new();
        Mock<IRuleMetadataService> metadata = new();
        ListedFirewallRule deleted = new()
        {
            RuleId = "sha256:deleted",
            Parsed = true,
            RawLine = "deleted",
            Rule = new FirewallRuleSpecification(),
        };
        RuleMutationResponse expected = new(IntentOperations.DELETE_RULE, deleted);
        client.Setup(static c => c.SendAsync<DeleteRuleRequest, RuleMutationResponse>(
                It.IsAny<DeleteRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        RulesController controller = CreateController(client.Object, metadata: metadata.Object);

        ActionResult<RuleMutationResponse> result = await controller.DeleteRuleAsync(
            CreateSignedDelete(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        metadata.Verify(service => service.RemoveForDeletedRuleAsync("sha256:deleted", It.IsAny<CancellationToken>()), Times.Once);
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

    private static RulesController CreateController(
        IUfwClient client,
        IRuleInventoryService? inventory = null,
        IRuleMetadataService? metadata = null)
    {
        inventory ??= new Mock<IRuleInventoryService>().Object;
        metadata ??= new Mock<IRuleMetadataService>().Object;
        RulesController controller = new(client, inventory, metadata, new DaemonApiErrorMapper())
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

    private static InsertRuleRequest CreateSignedInsert() => new()
    {
        Version = 1,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce",
        Operation = IntentOperations.INSERT_RULE,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            anchorOccurrenceId = 0,
            placement = "Before",
            rule = new { action = "Allow", addressFamily = "IPv4", direction = "In", protocol = "Tcp", destinationPorts = "22" },
        }),
        Signature = "sig",
    };

    private static RuleInsertionResponse CreateInsertionResponse(RuleInsertionOutcome outcome) => new(
        outcome,
        new RuleListResponse(Active: true, [], TestFirewallConfiguration.Enabled),
        null,
        "diagnostic");

    private static ReorderRulesRequest CreateSignedReorder() => new()
    {
        Version = 1,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce",
        Operation = IntentOperations.REORDER_RULES,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            desiredOrder = new[] { 1, 0 },
        }),
        Signature = "sig",
    };

    private static RuleReorderResponse CreateReorderResponse(RuleReorderOutcome outcome) => new(
        outcome,
        new RuleListResponse(Active: true, [], TestFirewallConfiguration.Enabled),
        [new RuleReorderOperationResponse(
            new RuleReorderMoveResponse(1, 0, 0),
            RuleReorderOperationOutcome.FailedAndRestored,
            "diagnostic")],
        [new RuleReorderMoveResponse(0, 1, null)],
        [new RuleReorderMoveResponse(0, 1, null)],
        "diagnostic");

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
