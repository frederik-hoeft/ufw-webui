using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class RulesControllerIntegrationTests : ControllerIntegrationTest<RulesController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task ReorderRulesAsync_PreservesStructuredPartialReportThroughControllerPipelineAsync() =>
        UsingComponentAsync(CreateRequest(), async (controller, request, serviceProvider, cancellationToken) =>
        {
            IntegrationUfwClient daemon = serviceProvider.GetRequiredService<IntegrationUfwClient>();
            RuleListResponse finalSnapshot = new(Active: true, []);
            daemon.ReorderResponse = new RuleReorderResponse(
                RuleReorderOutcome.PartiallyCompleted,
                finalSnapshot,
                [new RuleReorderOperationResponse(
                    new RuleReorderMoveResponse(1, 0, 0),
                    RuleReorderOperationOutcome.FailedAndRestored,
                    "restored")],
                [new RuleReorderMoveResponse(0, 1, null)],
                [],
                "state diverged");

            ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, cancellationToken);

            ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
            Assert.AreEqual(StatusCodes.Status409Conflict, response.StatusCode);
            RuleReorderResponse report = Assert.IsInstanceOfType<RuleReorderResponse>(response.Value);
            Assert.AreSame(daemon.ReorderResponse, report);
            Assert.AreSame(request, daemon.LastReorderRequest);
            Assert.AreSame(finalSnapshot, report.FinalSnapshot);
            Assert.HasCount(1, report.Operations);
            Assert.HasCount(1, report.BlockedOperations);
        }, TestContext.CancellationToken);

    private static ReorderRulesRequest CreateRequest()
    {
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            DesiredOrder = [],
        };
        return new ReorderRulesRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = "deployment",
            KeyId = "sha256:key",
            IssuedAtUnix = 1,
            Nonce = "nonce",
            Operation = IntentOperations.REORDER_RULES,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.ReorderRulesPayload),
            Signature = "signature",
        };
    }
}
