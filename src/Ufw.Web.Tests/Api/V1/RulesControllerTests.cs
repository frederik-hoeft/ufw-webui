using Ufw.Shared.Management.Rules;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RulesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TestGetRulesAsync_ReturnsEnrichedInventoryAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
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
        RuleInventoryResponse expected = new(firewall, [new RuleMetadataItem(metadataId, "sha256:abc", "ssh", [new RuleTagItem(tagId, "prod", "#336699")])]);
        inventory.Setup(service => service.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        RulesController controller = CreateController(daemonRules.Object, inventory.Object);
        ActionResult<RuleInventoryResponse> result = await controller.GetRulesAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task TestUpdateMetadataAsync_ReturnsServiceResultAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        UpdateRuleMetadataRequest request = new() { TagIds = [tagId] };
        RuleMetadataMutationResponse expected = new(new RuleMetadataItem(metadataId, "sha256:abc", null, [new RuleTagItem(tagId, "prod", "#336699")]));
        metadata.Setup(service => service.UpdateAsync("sha256:abc", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.Success, expected));

        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);
        ActionResult<RuleMetadataMutationResponse> result = await controller.UpdateMetadataAsync("sha256:abc", request, TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task TestUpdateMetadataAsync_MapsMissingAndInvalidMetadataAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        UpdateRuleMetadataRequest missingRequest = new();
        UpdateRuleMetadataRequest invalidRequest = new();
        UpdateRuleMetadataRequest missingTagRequest = new();
        UpdateRuleMetadataRequest missingGroupRequest = new();
        metadata.Setup(service => service.UpdateAsync("missing", missingRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.RuleNotFound));
        metadata.Setup(service => service.UpdateAsync("invalid", invalidRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.InvalidMetadata));
        metadata.Setup(service => service.UpdateAsync("missing-tag", missingTagRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.TagNotFound));
        metadata.Setup(service => service.UpdateAsync("missing-group", missingGroupRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleMetadataUpdateResult(RuleMetadataUpdateOutcome.GroupNotFound));
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleMetadataMutationResponse> missing = await controller.UpdateMetadataAsync("missing", missingRequest, TestContext.CancellationToken);
        ActionResult<RuleMetadataMutationResponse> invalid = await controller.UpdateMetadataAsync("invalid", invalidRequest, TestContext.CancellationToken);
        ActionResult<RuleMetadataMutationResponse> missingTag = await controller.UpdateMetadataAsync("missing-tag", missingTagRequest, TestContext.CancellationToken);
        ActionResult<RuleMetadataMutationResponse> missingGroup = await controller.UpdateMetadataAsync("missing-group", missingGroupRequest, TestContext.CancellationToken);

        Assert.IsInstanceOfType<NotFoundResult>(missing.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(invalid.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(missingTag.Result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(missingGroup.Result);
    }

    [TestMethod]
    public async Task TestAddRuleAsync_ForwardsSignedEnvelopeAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        AddRuleRequest request = CreateSignedAdd();
        RuleMutationResponse expected = new(IntentOperations.ADD_RULE, null!);
        daemonRules.Setup(static c => c.AddRuleAsync(It.IsAny<AddRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));

        RulesController controller = CreateController(daemonRules.Object);
        ActionResult<RuleMutationResponse> result = await controller.AddRuleAsync(request, TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
        daemonRules.Verify(
            c => c.AddRuleAsync(
                It.Is<AddRuleRequest>(sent => sent.DeploymentId == request.DeploymentId
                    && sent.Nonce == request.Nonce
                    && sent.Signature == request.Signature),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task TestAddRuleAsync_RejectsWrongOperationAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        AddRuleRequest request = CreateSignedAdd() with { Operation = IntentOperations.DELETE_RULE };
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleMutationResponse> result = await controller.AddRuleAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        daemonRules.Verify(
            static c => c.AddRuleAsync(It.IsAny<AddRuleRequest>(), It.IsAny<CancellationToken>()),
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
        Mock<IRuleDaemonGateway> daemonRules = new();
        InsertRuleRequest request = CreateSignedInsert();
        RuleInsertionResponse expected = CreateInsertionResponse(RuleInsertionOutcome.Completed);
        daemonRules.Setup(static c => c.InsertRuleAsync(It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        daemonRules.Verify(c => c.InsertRuleAsync(
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
        Mock<IRuleDaemonGateway> daemonRules = new();
        RuleInsertionResponse expected = CreateInsertionResponse(outcome);
        daemonRules.Setup(static c => c.InsertRuleAsync(It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(CreateSignedInsert(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreSame(expected, response.Value);
    }

    [TestMethod]
    public async Task TestInsertRuleAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        InsertRuleRequest request = CreateSignedInsert() with { Operation = IntentOperations.ADD_RULE };
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        daemonRules.Verify(static c => c.InsertRuleAsync(It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task TestInsertRuleAsync_DaemonFailurePropagatesToExceptionBoundaryAsync()
    {
        UfwIpcException expected = new(StatusCodes.Status409Conflict, "Intent nonce has already been used.");
        Mock<IRuleDaemonGateway> daemonRules = new();
        daemonRules.Setup(static c => c.InsertRuleAsync(It.IsAny<InsertRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure<RuleInsertionResponse>(expected));
        RulesController controller = CreateController(daemonRules.Object);

        UfwIpcException actual = await Assert.ThrowsExactlyAsync<UfwIpcException>(
            () => controller.InsertRuleAsync(CreateSignedInsert(), TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
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
        Mock<IRuleDaemonGateway> daemonRules = new();
        ReorderRulesRequest request = CreateSignedReorder();
        RuleReorderResponse expected = CreateReorderResponse(RuleReorderOutcome.Completed);
        daemonRules
            .Setup(static c => c.ReorderRulesAsync(It.IsAny<ReorderRulesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));

        RulesController controller = CreateController(daemonRules.Object);
        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        daemonRules.Verify(
            c => c.ReorderRulesAsync(
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
        Mock<IRuleDaemonGateway> daemonRules = new();
        RuleReorderResponse expected = CreateReorderResponse(outcome);
        daemonRules
            .Setup(static c => c.ReorderRulesAsync(It.IsAny<ReorderRulesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(CreateSignedReorder(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreSame(expected, response.Value);
    }

    [TestMethod]
    public async Task TestReorderRulesAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        ReorderRulesRequest request = CreateSignedReorder() with { Operation = IntentOperations.ADD_RULE };
        RulesController controller = CreateController(daemonRules.Object);

        ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        daemonRules.Verify(
            static c => c.ReorderRulesAsync(It.IsAny<ReorderRulesRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task TestReorderRulesAsync_DaemonFailurePropagatesToExceptionBoundaryAsync()
    {
        UfwIpcException expected = new(StatusCodes.Status409Conflict, "Intent nonce has already been used.");
        Mock<IRuleDaemonGateway> daemonRules = new();
        daemonRules
            .Setup(static c => c.ReorderRulesAsync(It.IsAny<ReorderRulesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure<RuleReorderResponse>(expected));
        RulesController controller = CreateController(daemonRules.Object);

        UfwIpcException actual = await Assert.ThrowsExactlyAsync<UfwIpcException>(
            () => controller.ReorderRulesAsync(CreateSignedReorder(), TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void ReplaceRuleAsync_UsesDedicatedReplacementSubresource()
    {
        System.Reflection.MethodInfo? method = typeof(RulesController).GetMethod(nameof(RulesController.ReplaceRuleAsync));
        Assert.IsNotNull(method);
        object[] attributes = method.GetCustomAttributes(typeof(HttpPutAttribute), inherit: false);
        Assert.HasCount(1, attributes);
        HttpPutAttribute attribute = Assert.IsInstanceOfType<HttpPutAttribute>(attributes[0]);
        Assert.AreEqual("replace", attribute.Template);
    }

    [TestMethod]
    public async Task TestReplaceRuleAsync_ForwardsCompletedReplacementAndReconcilesMetadataAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        ReplaceRuleRequest request = CreateSignedReplace();
        RuleReplacementResponse firewall = CreateReplacementResponse(RuleReplacementOutcome.Completed);
        daemonRules.Setup(static c => c.ReplaceRuleAsync(It.IsAny<ReplaceRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(firewall));
        metadata.Setup(service => service.ReconcileReplacementAsync(request, firewall, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RuleReplacementMetadataReconciliationOutcome.Completed);
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleReplacementMutationResponse> result = await controller.ReplaceRuleAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        RuleReplacementMutationResponse report = Assert.IsInstanceOfType<RuleReplacementMutationResponse>(response.Value);
        Assert.AreSame(firewall, report.Firewall);
        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, report.MetadataReconciliation);
        Assert.IsNull(report.MetadataDiagnostic);
        daemonRules.Verify(
            c => c.ReplaceRuleAsync(
                It.Is<ReplaceRuleRequest>(sent => sent.DeploymentId == request.DeploymentId
                    && sent.Nonce == request.Nonce
                    && sent.Operation == request.Operation
                    && sent.Payload.GetRawText() == request.Payload.GetRawText()
                    && sent.Signature == request.Signature),
                It.IsAny<CancellationToken>()),
            Times.Once);
        metadata.Verify(service => service.ReconcileReplacementAsync(request, firewall, CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task TestReplaceRuleAsync_MetadataFailurePreservesCompletedFirewallResultAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        ReplaceRuleRequest request = CreateSignedReplace();
        RuleReplacementResponse firewall = CreateReplacementResponse(RuleReplacementOutcome.Completed);
        daemonRules.Setup(static c => c.ReplaceRuleAsync(It.IsAny<ReplaceRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(firewall));
        metadata.Setup(service => service.ReconcileReplacementAsync(request, firewall, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RuleReplacementMetadataReconciliationOutcome.Failed);
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleReplacementMutationResponse> result = await controller.ReplaceRuleAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, response.StatusCode);
        RuleReplacementMutationResponse report = Assert.IsInstanceOfType<RuleReplacementMutationResponse>(response.Value);
        Assert.AreSame(firewall, report.Firewall);
        Assert.AreEqual(RuleReplacementOutcome.Completed, report.Firewall.Outcome);
        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Failed, report.MetadataReconciliation);
        StringAssert.Contains(report.MetadataDiagnostic, "metadata reconciliation failed");
    }

    [TestMethod]
    [DataRow(RuleReplacementOutcome.StaleBaseline, StatusCodes.Status409Conflict)]
    [DataRow(RuleReplacementOutcome.PreconditionFailed, StatusCodes.Status422UnprocessableEntity)]
    [DataRow(RuleReplacementOutcome.PartiallyCompleted, StatusCodes.Status409Conflict)]
    [DataRow(RuleReplacementOutcome.StateUncertain, StatusCodes.Status503ServiceUnavailable)]
    public async Task TestReplaceRuleAsync_NonCompletedFirewallOutcomeLeavesMetadataUntouchedAsync(RuleReplacementOutcome outcome, int expectedStatusCode)
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        RuleReplacementResponse firewall = CreateReplacementResponse(outcome);
        daemonRules.Setup(static c => c.ReplaceRuleAsync(It.IsAny<ReplaceRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(firewall));
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleReplacementMutationResponse> result = await controller.ReplaceRuleAsync(CreateSignedReplace(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        RuleReplacementMutationResponse report = Assert.IsInstanceOfType<RuleReplacementMutationResponse>(response.Value);
        Assert.AreSame(firewall, report.Firewall);
        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.NotAttempted, report.MetadataReconciliation);
        metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestReplaceRuleAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        ReplaceRuleRequest request = CreateSignedReplace() with { Operation = IntentOperations.ADD_RULE };
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleReplacementMutationResponse> result = await controller.ReplaceRuleAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        daemonRules.Verify(
            static c => c.ReplaceRuleAsync(It.IsAny<ReplaceRuleRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void BatchDeleteRulesAsync_UsesDedicatedBatchSubresource()
    {
        System.Reflection.MethodInfo? method = typeof(RulesController).GetMethod(nameof(RulesController.BatchDeleteRulesAsync));
        Assert.IsNotNull(method);
        object[] attributes = method.GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: false);
        Assert.HasCount(1, attributes);
        HttpDeleteAttribute attribute = Assert.IsInstanceOfType<HttpDeleteAttribute>(attributes[0]);
        Assert.AreEqual("batch", attribute.Template);
    }

    [TestMethod]
    public async Task TestBatchDeleteRulesAsync_ForwardsSignedEnvelopeAndReconcilesMetadataAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        BatchDeleteRulesRequest request = CreateSignedBatchDelete();
        RuleBatchDeleteResponse expected = CreateBatchDeleteResponse(RuleBatchDeleteOutcome.Completed);
        daemonRules
            .Setup(static c => c.BatchDeleteRulesAsync(It.IsAny<BatchDeleteRulesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleBatchDeleteResponse> result = await controller.BatchDeleteRulesAsync(request, TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        daemonRules.Verify(
            c => c.BatchDeleteRulesAsync(
                It.Is<BatchDeleteRulesRequest>(sent => sent.DeploymentId == request.DeploymentId
                    && sent.Nonce == request.Nonce
                    && sent.Operation == request.Operation
                    && sent.Payload.GetRawText() == request.Payload.GetRawText()
                    && sent.Signature == request.Signature),
                It.IsAny<CancellationToken>()),
            Times.Once);
        metadata.Verify(service => service.ReconcileBatchDeleteAsync(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(RuleBatchDeleteOutcome.StaleBaseline, StatusCodes.Status409Conflict)]
    [DataRow(RuleBatchDeleteOutcome.PreconditionFailed, StatusCodes.Status422UnprocessableEntity)]
    [DataRow(RuleBatchDeleteOutcome.PartiallyCompleted, StatusCodes.Status409Conflict)]
    [DataRow(RuleBatchDeleteOutcome.StateUncertain, StatusCodes.Status503ServiceUnavailable)]
    public async Task TestBatchDeleteRulesAsync_PreservesStructuredNonSuccessReportAsync(RuleBatchDeleteOutcome outcome, int expectedStatusCode)
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        RuleBatchDeleteResponse expected = CreateBatchDeleteResponse(outcome);
        daemonRules
            .Setup(static c => c.BatchDeleteRulesAsync(It.IsAny<BatchDeleteRulesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleBatchDeleteResponse> result = await controller.BatchDeleteRulesAsync(CreateSignedBatchDelete(), TestContext.CancellationToken);

        ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(expectedStatusCode, response.StatusCode);
        Assert.AreSame(expected, response.Value);
        metadata.Verify(service => service.ReconcileBatchDeleteAsync(expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TestBatchDeleteRulesAsync_RejectsWrongOperationBeforeIpcAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        BatchDeleteRulesRequest request = CreateSignedBatchDelete() with { Operation = IntentOperations.DELETE_RULE };
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleBatchDeleteResponse> result = await controller.BatchDeleteRulesAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
        daemonRules.Verify(
            static c => c.BatchDeleteRulesAsync(It.IsAny<BatchDeleteRulesRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        metadata.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TestDeleteRuleAsync_RemovesMetadataForAuthoritativeDeletedIdentityAsync()
    {
        Mock<IRuleDaemonGateway> daemonRules = new();
        Mock<IRuleMetadataService> metadata = new();
        ListedFirewallRule deleted = new()
        {
            RuleId = "sha256:deleted",
            Parsed = true,
            RawLine = "deleted",
            Rule = new FirewallRuleSpecification(),
        };
        RuleMutationResponse expected = new(IntentOperations.DELETE_RULE, deleted);
        daemonRules.Setup(static c => c.DeleteRuleAsync(It.IsAny<DeleteRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));
        RulesController controller = CreateController(daemonRules.Object, metadata: metadata.Object);

        ActionResult<RuleMutationResponse> result = await controller.DeleteRuleAsync(CreateSignedDelete(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        metadata.Verify(service => service.RemoveForDeletedRuleAsync("sha256:deleted", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task TestDeleteRuleAsync_DaemonFailurePropagatesToExceptionBoundaryAsync()
    {
        UfwIpcException expected = new(StatusCodes.Status409Conflict, "A semantically identical rule already exists.");
        Mock<IRuleDaemonGateway> daemonRules = new();
        daemonRules
            .Setup(static c => c.DeleteRuleAsync(It.IsAny<DeleteRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure<RuleMutationResponse>(expected));

        RulesController controller = CreateController(daemonRules.Object);
        UfwIpcException actual = await Assert.ThrowsExactlyAsync<UfwIpcException>(
            () => controller.DeleteRuleAsync(CreateSignedDelete(), TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    private static RulesController CreateController(
        IRuleDaemonGateway daemonRules,
        IRuleInventoryService? inventory = null,
        IRuleMetadataService? metadata = null)
    {
        inventory ??= new Mock<IRuleInventoryService>().Object;
        metadata ??= new Mock<IRuleMetadataService>().Object;
        RulesController controller = new(daemonRules, inventory, metadata)
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
        [new RuleReorderOperationResponse(new RuleReorderMoveResponse(1, 0, 0), RuleReorderOperationOutcome.FailedAndRestored, "diagnostic")],
        [new RuleReorderMoveResponse(0, 1, null)],
        [new RuleReorderMoveResponse(0, 1, null)],
        "diagnostic");

    private static ReplaceRuleRequest CreateSignedReplace() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce-replace",
        Operation = IntentOperations.REPLACE_RULE,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            targetOccurrenceId = 0,
            originalRuleId = "sha256:original",
            replacementRule = new { action = "Allow", addressFamily = "IPv4", direction = "In", protocol = "Tcp", destinationPorts = "443" },
        }),
        Signature = "sig",
    };

    private static RuleReplacementResponse CreateReplacementResponse(RuleReplacementOutcome outcome)
    {
        ListedFirewallRule? replacement = outcome == RuleReplacementOutcome.Completed
            ? new ListedFirewallRule
            {
                RuleId = "sha256:replacement",
                DisplayNumber = 1,
                Parsed = true,
                RawLine = "replacement",
                Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
            }
            : null;
        return new RuleReplacementResponse(
            outcome,
            outcome == RuleReplacementOutcome.StateUncertain ? null : new RuleListResponse(Active: true, replacement is null ? [] : [replacement], TestFirewallConfiguration.Enabled),
            replacement,
            RecoveryOutcome: null,
            Diagnostic: "diagnostic");
    }

    private static BatchDeleteRulesRequest CreateSignedBatchDelete() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment-test",
        KeyId = "sha256:test",
        IssuedAtUnix = 1,
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULES_BATCH,
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new BatchDeleteRulesPayload
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            OccurrenceIds = [1, 3],
        }),
        Signature = "sig",
    };

    private static RuleBatchDeleteResponse CreateBatchDeleteResponse(RuleBatchDeleteOutcome outcome) => new(
        outcome,
        outcome is RuleBatchDeleteOutcome.StateUncertain ? null : new RuleListResponse(Active: true, [], TestFirewallConfiguration.Enabled),
        [new RuleBatchDeleteOperationResponse(3, "sha256:deleted", RuleBatchDeleteOperationOutcome.Deleted, null)],
        outcome is RuleBatchDeleteOutcome.Completed ? [] : [1],
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
