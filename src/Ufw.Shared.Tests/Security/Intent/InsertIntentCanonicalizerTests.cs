using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ufw.Roslyn.Controllers;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class InsertIntentCanonicalizerTests
{
    [TestMethod]
    public void CanonicalizeInsert_BindsPlacementAndCompleteRule()
    {
        TestIntent intent = CreateIntent();
        InsertRulePayload payload = CreatePayload();

        string canonical = Encoding.UTF8.GetString(IntentCanonicalizer.CanonicalizeInsert(intent, payload));

        Assert.AreEqual(
            "ufw-intent/2\n"
            + "deploymentId=deployment\n"
            + "keyId=key\n"
            + "issuedAtUnix=123\n"
            + "nonce=nonce\n"
            + "operation=rules.insert\n"
            + "payload:\n"
            + "baselineFingerprint=sha256:baseline\n"
            + "anchorOccurrenceId=2\n"
            + "placement=after\n"
            + "action=allow\n"
            + "addressFamily=ipv4\n"
            + "comment=admin\n"
            + "destination=10.0.0.10\n"
            + "destinationInterface=\n"
            + "destinationPorts=443\n"
            + "direction=in\n"
            + "protocol=tcp\n"
            + "source=10.0.0.0/24\n"
            + "sourceInterface=\n"
            + "sourcePorts=\n",
            canonical);
    }

    [TestMethod]
    public void CanonicalizeInsert_AnySignedFieldOrPayloadChange_ChangesBytes()
    {
        TestIntent intent = CreateIntent();
        InsertRulePayload payload = CreatePayload();
        byte[] baseline = IntentCanonicalizer.CanonicalizeInsert(intent, payload);

        AssertDifferent(baseline, intent with { Nonce = "different" }, payload);
        AssertDifferent(baseline, intent with { DeploymentId = "different" }, payload);
        AssertDifferent(baseline, intent with { Operation = IntentOperations.ADD_RULE }, payload);
        AssertDifferent(baseline, intent, Clone(payload, baselineFingerprint: "sha256:different"));
        AssertDifferent(baseline, intent, Clone(payload, anchorOccurrenceId: 1));
        AssertDifferent(baseline, intent, Clone(payload, placement: RuleInsertionPlacement.Before));

        InsertRulePayload changedRule = Clone(payload);
        changedRule.Rule.Comment = "different";
        AssertDifferent(baseline, intent, changedRule);
    }

    [TestMethod]
    public void InsertPayload_SourceGeneratedJsonContext_RoundTrips()
    {
        InsertRulePayload payload = CreatePayload();

        string json = JsonSerializer.Serialize(payload, MessageJsonSerializerContext.Default.InsertRulePayload);
        InsertRulePayload? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.InsertRulePayload);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.BaselineFingerprint, roundTrip.BaselineFingerprint);
        Assert.AreEqual(payload.AnchorOccurrenceId, roundTrip.AnchorOccurrenceId);
        Assert.AreEqual(payload.Placement, roundTrip.Placement);
        Assert.AreEqual(payload.Rule.AddressFamily, roundTrip.Rule.AddressFamily);
        Assert.AreEqual(payload.Rule.DestinationPorts, roundTrip.Rule.DestinationPorts);
    }

    [TestMethod]
    public void InsertRequest_SourceGeneratedJsonContext_RoundTrips()
    {
        InsertRuleRequest request = new()
        {
            DeploymentId = "deployment",
            KeyId = "key",
            IssuedAtUnix = 123,
            Nonce = "nonce",
            Operation = IntentOperations.INSERT_RULE,
            Payload = JsonSerializer.SerializeToElement(CreatePayload(), MessageJsonSerializerContext.Default.InsertRulePayload),
            Signature = "signature",
        };

        string json = JsonSerializer.Serialize(request, MessageJsonSerializerContext.Default.InsertRuleRequest);
        InsertRuleRequest? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.InsertRuleRequest);

        Assert.IsNotNull(roundTrip);
        IIdentifiable identifiable = roundTrip;
        Assert.AreEqual("POST", identifiable.Method);
        Assert.AreEqual("/api/v1/rules/insert", identifiable.Id);
        Assert.AreEqual(request.Operation, roundTrip.Operation);
        Assert.AreEqual(request.Signature, roundTrip.Signature);
    }

    [TestMethod]
    public void CreateInsertRequest_SignsCanonicalPayload()
    {
        using ECDsa key = IntentSigner.CreateP256();
        InsertRulePayload payload = CreatePayload();
        payload.BaselineFingerprint = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        TimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(456));

        InsertRuleRequest request = IntentRequestFactory.CreateInsertRequest(
            key,
            "deployment",
            payload,
            MessageJsonSerializerContext.Default.InsertRulePayload,
            timeProvider);

        Assert.AreEqual(IntentOperations.INSERT_RULE, request.Operation);
        Assert.AreEqual(456L, request.IssuedAtUnix);
        Assert.IsTrue(IntentSigner.Verify(key, IntentCanonicalizer.CanonicalizeInsert(request, payload), request.Signature));
    }

    private static void AssertDifferent(byte[] baseline, ISignedIntent intent, InsertRulePayload payload) =>
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeInsert(intent, payload)));

    private static InsertRulePayload CreatePayload() => new()
    {
        BaselineFingerprint = "sha256:baseline",
        AnchorOccurrenceId = 2,
        Placement = RuleInsertionPlacement.After,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "10.0.0.0/24",
            Destination = "10.0.0.10",
            DestinationPorts = "443",
            Comment = "admin",
        },
    };

    private static InsertRulePayload Clone(
        InsertRulePayload source,
        string? baselineFingerprint = null,
        int? anchorOccurrenceId = null,
        RuleInsertionPlacement? placement = null) => new()
        {
            BaselineFingerprint = baselineFingerprint ?? source.BaselineFingerprint,
            AnchorOccurrenceId = anchorOccurrenceId ?? source.AnchorOccurrenceId,
            Placement = placement ?? source.Placement,
            Rule = new FirewallRuleSpecification
            {
                Action = source.Rule.Action,
                AddressFamily = source.Rule.AddressFamily,
                Direction = source.Rule.Direction,
                Protocol = source.Rule.Protocol,
                Source = source.Rule.Source,
                SourcePorts = source.Rule.SourcePorts,
                SourceInterface = source.Rule.SourceInterface,
                Destination = source.Rule.Destination,
                DestinationPorts = source.Rule.DestinationPorts,
                DestinationInterface = source.Rule.DestinationInterface,
                Comment = source.Rule.Comment,
            },
        };

    private static TestIntent CreateIntent() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.INSERT_RULE,
        Payload = JsonSerializer.SerializeToElement(new { }),
        Signature = "signature",
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record TestIntent : ISignedIntent
    {
        public int Version { get; init; }

        public required string DeploymentId { get; init; }

        public required string KeyId { get; init; }

        public long IssuedAtUnix { get; init; }

        public required string Nonce { get; init; }

        public required string Operation { get; init; }

        public JsonElement Payload { get; init; }

        public required string Signature { get; init; }
    }
}
