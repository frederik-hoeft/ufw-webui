using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Management.Rules;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;
using Ufw.Web.Services.Rules;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
internal sealed class RulesControllerIntegrationTests : ControllerIntegrationTest<RulesController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task InsertRuleAsync_PreservesStructuredUncertainReportThroughControllerPipelineAsync() =>
        UsingComponentAsync(CreateInsertRequest(), async (controller, request, serviceProvider, cancellationToken) =>
        {
            IntegrationUfwClient daemon = serviceProvider.GetRequiredService<IntegrationUfwClient>();
            RuleListResponse finalSnapshot = new(Active: true, [Listed("existing", 1)], TestFirewallConfiguration.Enabled);
            daemon.InsertResponse = new RuleInsertionResponse(RuleInsertionOutcome.StateUncertain, finalSnapshot, InsertedRule: null, Diagnostic: "state diverged");

            ActionResult<RuleInsertionResponse> result = await controller.InsertRuleAsync(request, cancellationToken);

            ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
            Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
            RuleInsertionResponse report = Assert.IsInstanceOfType<RuleInsertionResponse>(response.Value);
            Assert.AreSame(daemon.InsertResponse, report);
            Assert.IsNotNull(daemon.LastInsertRequest);
            Assert.AreEqual(request.DeploymentId, daemon.LastInsertRequest.DeploymentId);
            Assert.AreEqual(request.Nonce, daemon.LastInsertRequest.Nonce);
            Assert.AreEqual(request.Payload.GetRawText(), daemon.LastInsertRequest.Payload.GetRawText());
            Assert.AreEqual(request.Signature, daemon.LastInsertRequest.Signature);
            Assert.AreSame(finalSnapshot, report.FinalSnapshot);
            Assert.IsNull(report.InsertedRule);
        }, TestContext.CancellationToken);

    [TestMethod]
    public async Task ReplaceRuleAsync_RekeysMetadataFromDaemonConfirmedResultAsync()
    {
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        ReplaceRuleIntentRequest replacementRequest = CreateReplaceRequest(originalRuleId, replacementRule);

        await UsingComponentAsync(replacementRequest, async (controller, request, serviceProvider, cancellationToken) =>
        {
            IRuleMetadataDataAccess metadata = serviceProvider.GetRequiredService<IRuleMetadataDataAccess>();
            DataMutationResult<RuleMetadataItem?> saved = await metadata.SaveAsync(originalRuleId, new RuleMetadataValues("source", [], GroupId: null), cancellationToken);
            Assert.IsTrue(saved.IsSuccess);
            Assert.IsNotNull(saved.Value);

            IntegrationUfwClient daemon = serviceProvider.GetRequiredService<IntegrationUfwClient>();
            ListedFirewallRule replacement = new()
            {
                RuleId = replacementRuleId,
                DisplayNumber = 1,
                Parsed = true,
                RawLine = replacementRuleId,
                Rule = replacementRule,
            };
            daemon.ReplaceResponse = new RuleReplacementResponse(
                RuleReplacementOutcome.Completed,
                new RuleListResponse(Active: true, [replacement], TestFirewallConfiguration.Enabled),
                replacement,
                RecoveryOutcome: null,
                Diagnostic: null);

            ActionResult<RuleReplacementMutationResponse> result = await controller.ReplaceRuleAsync(request, cancellationToken);

            ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
            Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
            RuleReplacementMutationResponse report = Assert.IsInstanceOfType<RuleReplacementMutationResponse>(response.Value);
            Assert.AreSame(daemon.ReplaceResponse, report.Firewall);
            Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, report.MetadataReconciliation);
            Assert.IsNotNull(daemon.LastReplaceRequest);
            Assert.AreEqual(request.DeploymentId, daemon.LastReplaceRequest.DeploymentId);
            Assert.AreEqual(request.Nonce, daemon.LastReplaceRequest.Nonce);
            Assert.AreEqual(request.Payload.GetRawText(), daemon.LastReplaceRequest.Payload.GetRawText());
            Assert.AreEqual(request.Signature, daemon.LastReplaceRequest.Signature);
            IReadOnlyList<RuleMetadataItem> reconciled = await metadata.GetForRuleIdsAsync([originalRuleId, replacementRuleId], cancellationToken);
            Assert.HasCount(1, reconciled);
            Assert.AreEqual(replacementRuleId, reconciled[0].RuleId);
            Assert.AreEqual(saved.Value.Id, reconciled[0].Id);
            Assert.AreEqual("source", reconciled[0].Notes);
        }, TestContext.CancellationToken);
    }

    [TestMethod]
    public Task ReorderRulesAsync_PreservesStructuredPartialReportThroughControllerPipelineAsync() =>
        UsingComponentAsync(CreateRequest(), async (controller, request, serviceProvider, cancellationToken) =>
        {
            IntegrationUfwClient daemon = serviceProvider.GetRequiredService<IntegrationUfwClient>();
            RuleListResponse finalSnapshot = new(Active: true, [], TestFirewallConfiguration.Enabled);
            daemon.ReorderResponse = new RuleReorderResponse(
                RuleReorderOutcome.PartiallyCompleted,
                finalSnapshot,
                [new RuleReorderOperationResponse(new RuleReorderMoveResponse(1, 0, 0), RuleReorderOperationOutcome.FailedAndRestored, "restored")],
                [new RuleReorderMoveResponse(0, 1, null)],
                [],
                "state diverged");

            ActionResult<RuleReorderResponse> result = await controller.ReorderRulesAsync(request, cancellationToken);

            ObjectResult response = Assert.IsInstanceOfType<ObjectResult>(result.Result);
            Assert.AreEqual(StatusCodes.Status409Conflict, response.StatusCode);
            RuleReorderResponse report = Assert.IsInstanceOfType<RuleReorderResponse>(response.Value);
            Assert.AreSame(daemon.ReorderResponse, report);
            Assert.IsNotNull(daemon.LastReorderRequest);
            Assert.AreEqual(request.DeploymentId, daemon.LastReorderRequest.DeploymentId);
            Assert.AreEqual(request.Nonce, daemon.LastReorderRequest.Nonce);
            Assert.AreEqual(request.Payload.GetRawText(), daemon.LastReorderRequest.Payload.GetRawText());
            Assert.AreEqual(request.Signature, daemon.LastReorderRequest.Signature);
            Assert.AreSame(finalSnapshot, report.FinalSnapshot);
            Assert.HasCount(1, report.Operations);
            Assert.HasCount(1, report.BlockedOperations);
        }, TestContext.CancellationToken);

    private static FirewallRuleSpecification Rule(string destinationPort) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = destinationPort,
    };

    private static ReplaceRuleIntentRequest CreateReplaceRequest(string originalRuleId, FirewallRuleSpecification replacementRule)
    {
        ReplaceRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            TargetOccurrenceId = 0,
            OriginalRuleId = originalRuleId,
            ReplacementRule = replacementRule,
        };
        return new ReplaceRuleIntentRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = "deployment",
            KeyId = "sha256:key",
            IssuedAtUnix = 1,
            Nonce = "nonce-replace",
            Operation = IntentOperations.REPLACE_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.ReplaceRulePayload),
            Signature = "signature",
        };
    }

    private static InsertRuleIntentRequest CreateInsertRequest()
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
        return new InsertRuleIntentRequest
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

    private static ReorderRulesIntentRequest CreateRequest()
    {
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            DesiredOrder = [],
        };
        return new ReorderRulesIntentRequest
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
