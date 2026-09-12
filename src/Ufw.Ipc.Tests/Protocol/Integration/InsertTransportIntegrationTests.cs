using System.Text.Json;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class InsertTransportIntegrationTests : IpcProtocolTestBase
{
    private const string ROUTE = "/api/v1/rules/insert";

    [TestMethod]
    public Task InsertRequestAndStructuredResult_RoundTripThroughProductionProtocolAsync()
    {
        InsertRuleRequest request = CreateRequest();
        RuleInsertionResponse expected = CreateResponse();
        InsertRuleRequest? received = null;

        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPost<InsertRuleRequest, RuleInsertionResponse>(
                ROUTE,
                (candidate, _) =>
                {
                    received = candidate;
                    return ValueTask.FromResult(expected);
                }),
            actAsync: async (context, cancellationToken) =>
            {
                RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
                    request,
                    cancellationToken);

                Assert.IsNotNull(received);
                Assert.AreEqual(request.DeploymentId, received.DeploymentId);
                Assert.AreEqual(request.Nonce, received.Nonce);
                Assert.AreEqual(request.Operation, received.Operation);
                Assert.AreEqual(request.Signature, received.Signature);
                InsertRulePayload? receivedPayload = received.Payload.Deserialize(
                    MessageJsonSerializerContext.Default.InsertRulePayload);
                Assert.IsNotNull(receivedPayload);
                Assert.AreEqual(CreatePayload().BaselineFingerprint, receivedPayload.BaselineFingerprint);
                Assert.AreEqual(1, receivedPayload.AnchorOccurrenceId);
                Assert.AreEqual(RuleInsertionPlacement.After, receivedPayload.Placement);
                Assert.AreEqual(FirewallAddressFamily.IPv4, receivedPayload.Rule.AddressFamily);

                Assert.AreEqual(expected.Outcome, response.Outcome);
                Assert.AreEqual(expected.Diagnostic, response.Diagnostic);
                Assert.IsNotNull(response.FinalSnapshot);
                Assert.HasCount(2, response.FinalSnapshot.Rules);
                Assert.IsNotNull(response.InsertedRule);
                Assert.AreEqual("sha256:inserted", response.InsertedRule.RuleId);
            },
            cancellationToken: TestContext.CancellationToken).AsTask();
    }

    [TestMethod]
    [DataRow(RuleInsertionOutcome.StaleBaseline)]
    [DataRow(RuleInsertionOutcome.PreconditionFailed)]
    [DataRow(RuleInsertionOutcome.StateUncertain)]
    public Task NonSuccessInsertionOutcome_RoundTripsThroughProductionProtocolAsync(RuleInsertionOutcome outcome)
    {
        RuleInsertionResponse expected = new(outcome, new RuleListResponse(Active: true, []), null, "diagnostic");
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPost<InsertRuleRequest, RuleInsertionResponse>(
                ROUTE,
                (_, _) => ValueTask.FromResult(expected)),
            actAsync: async (context, cancellationToken) =>
            {
                RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(
                    CreateRequest(),
                    cancellationToken);

                Assert.AreEqual(outcome, response.Outcome);
                Assert.IsNotNull(response.FinalSnapshot);
                Assert.IsNull(response.InsertedRule);
                Assert.AreEqual("diagnostic", response.Diagnostic);
            },
            cancellationToken: TestContext.CancellationToken).AsTask();
    }

    private static InsertRuleRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment-test",
        KeyId = "sha256:key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.INSERT_RULE,
        Payload = JsonSerializer.SerializeToElement(
            CreatePayload(),
            MessageJsonSerializerContext.Default.InsertRulePayload),
        Signature = "signature",
    };

    private static InsertRulePayload CreatePayload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        AnchorOccurrenceId = 1,
        Placement = RuleInsertionPlacement.After,
        Rule = Rule(),
    };

    private static RuleInsertionResponse CreateResponse()
    {
        ListedFirewallRule baseline = Listed("sha256:baseline", 1, "80");
        ListedFirewallRule inserted = Listed("sha256:inserted", 2, "22");
        return new RuleInsertionResponse(
            RuleInsertionOutcome.Completed,
            new RuleListResponse(Active: true, [baseline, inserted]),
            inserted,
            "completed");
    }

    private static ListedFirewallRule Listed(string ruleId, int displayNumber, string port) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = $"[ {displayNumber}] {port}/tcp ALLOW IN Anywhere",
        Rule = Rule(port),
    };

    private static FirewallRuleSpecification Rule(string port = "22") => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = port,
    };
}
