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
    public Task InsertRuleAsync_PreservesStructuredUncertainReportThroughControllerPipelineAsync() =>
        UsingComponentAsync(CreateInsertRequest(), async (controller, request, serviceProvider, cancellationToken) =>
        {
            IntegrationUfwClient daemon = serviceProvider.GetRequiredService<IntegrationUfwClient>();
            RuleListResponse finalSnapshot = new(Active: true, [Listed("existing", 1)]);
            daemon.InsertResponse = new RuleInsertionResponse(
                RuleInsertionOutcome.StateUncertain,
                finalSnapshot,
                InsertedRule: null,
                Diagnostic: "state diverged");

            ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, cancellationToken);

            ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
            Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
            RuleInsertionResponse report = Assert.IsInstanceOfType<RuleInsertionResponse>(response.Value);
            Assert.AreSame(daemon.InsertResponse, report);
            Assert.AreSame(request, daemon.LastInsertRequest);
            Assert.AreSame(finalSnapshot, report.FinalSnapshot);
            Assert.IsNull(report.InsertedRule);
        }, TestContext.CancellationToken);

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

    private static InsertRuleRequest CreateInsertRequest()
    {
        InsertRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, [Listed("existing", 1)]),
            AnchorOccurrenceId = 0,
            Placement = RuleInsertionPlacement.Before,
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv4,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                DestinationPorts = "22",
            },
        };
        return new InsertRuleRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = "deployment",
            KeyId = "sha256:key",
            IssuedAtUnix = 1,
            Nonce = "nonce-insert",
            Operation = IntentOperations.INSERT_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.InsertRulePayload),
            Signature = "signature",
        };
    }

    private static ListedFirewallRule Listed(string id, int displayNumber) => new()
    {
        RuleId = id,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "80",
        },
    };

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
