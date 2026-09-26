using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected arrays are local one-shot test assertions.")]
public sealed class BatchDeleteTransportIntegrationTests : IpcProtocolTestBase
{
    private const string ROUTE = "/api/v1/rules/batch";

    [TestMethod]
    public Task BatchDeleteRequestAndStructuredResult_RoundTripThroughProductionProtocolAsync()
    {
        BatchDeleteRulesRequest request = CreateRequest();
        RuleBatchDeleteResponse expected = CreateResponse();
        BatchDeleteRulesRequest? received = null;

        return RunAsync(
            configureEndpoints: endpoints => endpoints.Map<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(
                RequestMethod.Delete.ToString(),
                ROUTE,
                (_, candidate, _) =>
                {
                    received = candidate;
                    return ValueTask.FromResult(expected);
                }),
            actAsync: async (context, cancellationToken) =>
            {
                RuleBatchDeleteResponse response = await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

                Assert.IsNotNull(received);
                Assert.AreEqual(request.DeploymentId, received.DeploymentId);
                Assert.AreEqual(request.Nonce, received.Nonce);
                Assert.AreEqual(request.Operation, received.Operation);
                Assert.AreEqual(request.Signature, received.Signature);
                BatchDeleteRulesPayload? receivedPayload = received.Payload.Deserialize(MessageJsonSerializerContext.Default.BatchDeleteRulesPayload);
                Assert.IsNotNull(receivedPayload);
                Assert.AreEqual(CreatePayload().BaselineFingerprint, receivedPayload.BaselineFingerprint);
                CollectionAssert.AreEqual(CreatePayload().OccurrenceIds, receivedPayload.OccurrenceIds);

                Assert.AreEqual(expected.Outcome, response.Outcome);
                Assert.AreEqual(expected.Diagnostic, response.Diagnostic);
                Assert.IsNotNull(response.FinalSnapshot);
                Assert.HasCount(1, response.FinalSnapshot.Rules);
                Assert.HasCount(2, response.Operations);
                Assert.AreEqual(RuleBatchDeleteOperationOutcome.DeletedAfterProcessFailure, response.Operations[0].Outcome);
                CollectionAssert.AreEqual(new[] { 1 }, response.PendingOccurrenceIds.ToArray());
            },
            cancellationToken: TestContext.CancellationToken).AsTask();
    }

    private static BatchDeleteRulesRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment-test",
        KeyId = "sha256:key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULES_BATCH,
        Payload = JsonSerializer.SerializeToElement(CreatePayload(), MessageJsonSerializerContext.Default.BatchDeleteRulesPayload),
        Signature = "signature",
    };

    private static BatchDeleteRulesPayload CreatePayload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        OccurrenceIds = [1, 3],
    };

    private static RuleBatchDeleteResponse CreateResponse()
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
        return new RuleBatchDeleteResponse(
            RuleBatchDeleteOutcome.PartiallyCompleted,
            new RuleListResponse(Active: true, [rule], TestFirewallConfiguration.Enabled),
            [
                new RuleBatchDeleteOperationResponse(3, "sha256:deleted-3", RuleBatchDeleteOperationOutcome.DeletedAfterProcessFailure, "process failed"),
                new RuleBatchDeleteOperationResponse(2, "sha256:deleted-2", RuleBatchDeleteOperationOutcome.Deleted, null),
            ],
            [1],
            "state diverged");
    }
}
