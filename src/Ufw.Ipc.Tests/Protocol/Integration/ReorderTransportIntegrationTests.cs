using System.Text.Json;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class ReorderTransportIntegrationTests : IpcProtocolTestBase
{
    private const string ROUTE = "/api/v1/rules/order";

    [TestMethod]
    public Task ReorderRequestAndStructuredResult_RoundTripThroughProductionProtocolAsync()
    {
        ReorderRulesRequest request = CreateRequest();
        RuleReorderResponse expected = CreateResponse();
        ReorderRulesRequest? received = null;

        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPut<ReorderRulesRequest, RuleReorderResponse>(
                ROUTE,
                (candidate, _) =>
                {
                    received = candidate;
                    return ValueTask.FromResult(expected);
                }),
            actAsync: async (context, cancellationToken) =>
            {
                RuleReorderResponse response = await context.Client.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                    request,
                    cancellationToken);

                Assert.IsNotNull(received);
                Assert.AreEqual(request.DeploymentId, received.DeploymentId);
                Assert.AreEqual(request.Nonce, received.Nonce);
                Assert.AreEqual(request.Operation, received.Operation);
                Assert.AreEqual(request.Signature, received.Signature);
                ReorderRulesPayload? receivedPayload = received.Payload.Deserialize(
                    MessageJsonSerializerContext.Default.ReorderRulesPayload);
                Assert.IsNotNull(receivedPayload);
                Assert.AreEqual(CreatePayload().BaselineFingerprint, receivedPayload.BaselineFingerprint);
                CollectionAssert.AreEqual(CreatePayload().DesiredOrder, receivedPayload.DesiredOrder);

                Assert.AreEqual(expected.Outcome, response.Outcome);
                Assert.AreEqual(expected.Diagnostic, response.Diagnostic);
                Assert.IsNotNull(response.FinalSnapshot);
                Assert.AreEqual(1, response.FinalSnapshot.Rules.Count);
                Assert.AreEqual(1, response.Operations.Length);
                Assert.AreEqual(RuleReorderOperationOutcome.FailedAndRestored, response.Operations[0].Outcome);
                Assert.AreEqual(1, response.BlockedOperations.Length);
                Assert.AreEqual(1, response.PendingOperations.Length);
            },
            cancellationToken: TestContext.CancellationToken).AsTask();
    }

    private static ReorderRulesRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment-test",
        KeyId = "sha256:key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.REORDER_RULES,
        Payload = JsonSerializer.SerializeToElement(
            CreatePayload(),
            MessageJsonSerializerContext.Default.ReorderRulesPayload),
        Signature = "signature",
    };

    private static ReorderRulesPayload CreatePayload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        DesiredOrder = [1, 0],
    };

    private static RuleReorderResponse CreateResponse()
    {
        ListedFirewallRule rule = new()
        {
            DisplayNumber = 1,
            Parsed = true,
            RawLine = "[ 1] 22/tcp ALLOW IN Anywhere",
            RuleId = "sha256:rule",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv4,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                DestinationPorts = "22",
            },
        };
        RuleReorderMoveResponse move = new(1, 0, 0);
        return new RuleReorderResponse(
            RuleReorderOutcome.PartiallyCompleted,
            new RuleListResponse(Active: true, [rule]),
            [new RuleReorderOperationResponse(move, RuleReorderOperationOutcome.FailedAndRestored, "restored")],
            [new RuleReorderMoveResponse(0, 1, null)],
            [new RuleReorderMoveResponse(0, 1, null)],
            "state diverged");
    }
}
