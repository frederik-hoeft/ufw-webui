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
public sealed class ReplaceIntentCanonicalizerTests
{
    private const string ORIGINAL_RULE_ID = "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string VALID_FINGERPRINT = "sha256:AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE";

    [TestMethod]
    public void CanonicalizeReplace_BindsTargetIdentityAndCompleteReplacementRule()
    {
        TestIntent intent = CreateIntent();
        ReplaceRulePayload payload = CreatePayload();

        string canonical = Encoding.UTF8.GetString(IntentCanonicalizer.CanonicalizeReplace(intent, payload));

        Assert.AreEqual(
            "ufw-intent/2\n"
            + "deploymentId=deployment\n"
            + "keyId=key\n"
            + "issuedAtUnix=123\n"
            + "nonce=nonce\n"
            + "operation=rules.replace\n"
            + "payload:\n"
            + $"baselineFingerprint={VALID_FINGERPRINT}\n"
            + "targetOccurrenceId=2\n"
            + $"originalRuleId={ORIGINAL_RULE_ID}\n"
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
    public void CanonicalizeReplace_AnySignedFieldOrPayloadChange_ChangesBytes()
    {
        TestIntent intent = CreateIntent();
        ReplaceRulePayload payload = CreatePayload();
        byte[] baseline = IntentCanonicalizer.CanonicalizeReplace(intent, payload);

        AssertDifferent(baseline, intent with { Nonce = "different" }, payload);
        AssertDifferent(baseline, intent with { DeploymentId = "different" }, payload);
        AssertDifferent(baseline, intent with { Operation = IntentOperations.ADD_RULE }, payload);
        AssertDifferent(baseline, intent, Clone(payload, baselineFingerprint: "sha256:CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"));
        AssertDifferent(baseline, intent, Clone(payload, targetOccurrenceId: 1));
        AssertDifferent(baseline, intent, Clone(payload, originalRuleId: "sha256:DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD"));

        ReplaceRulePayload changedRule = Clone(payload);
        changedRule.ReplacementRule.Comment = "different";
        AssertDifferent(baseline, intent, changedRule);
    }

    [TestMethod]
    public void ReplacePayload_SourceGeneratedJsonContext_RoundTrips()
    {
        ReplaceRulePayload payload = CreatePayload();

        string json = JsonSerializer.Serialize(payload, MessageJsonSerializerContext.Default.ReplaceRulePayload);
        ReplaceRulePayload? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.ReplaceRulePayload);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.BaselineFingerprint, roundTrip.BaselineFingerprint);
        Assert.AreEqual(payload.TargetOccurrenceId, roundTrip.TargetOccurrenceId);
        Assert.AreEqual(payload.OriginalRuleId, roundTrip.OriginalRuleId);
        Assert.AreEqual(payload.ReplacementRule.AddressFamily, roundTrip.ReplacementRule.AddressFamily);
        Assert.AreEqual(payload.ReplacementRule.DestinationPorts, roundTrip.ReplacementRule.DestinationPorts);
    }

    [TestMethod]
    public void ReplaceRequest_SourceGeneratedJsonContext_RoundTrips()
    {
        ReplaceRuleRequest request = new()
        {
            DeploymentId = "deployment",
            KeyId = "key",
            IssuedAtUnix = 123,
            Nonce = "nonce",
            Operation = IntentOperations.REPLACE_RULE,
            Payload = JsonSerializer.SerializeToElement(CreatePayload(), MessageJsonSerializerContext.Default.ReplaceRulePayload),
            Signature = "signature",
        };

        string json = JsonSerializer.Serialize(request, MessageJsonSerializerContext.Default.ReplaceRuleRequest);
        ReplaceRuleRequest? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.ReplaceRuleRequest);

        Assert.IsNotNull(roundTrip);
        IIdentifiable identifiable = roundTrip;
        Assert.AreEqual("PUT", identifiable.Method);
        Assert.AreEqual("/api/v1/rules/replace", identifiable.Id);
        Assert.AreEqual(request.Operation, roundTrip.Operation);
        Assert.AreEqual(request.Signature, roundTrip.Signature);
    }

    [TestMethod]
    public void CreateReplaceRequest_SignsCanonicalPayload()
    {
        using ECDsa key = IntentSigner.CreateP256();
        ReplaceRulePayload payload = CreatePayload();
        TimeProvider timeProvider = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(456));

        ReplaceRuleRequest request = IntentRequestFactory.CreateReplaceRequest(key, "deployment", payload, MessageJsonSerializerContext.Default.ReplaceRulePayload, timeProvider);

        Assert.AreEqual(IntentOperations.REPLACE_RULE, request.Operation);
        Assert.AreEqual(456L, request.IssuedAtUnix);
        Assert.IsTrue(IntentSigner.Verify(key, IntentCanonicalizer.CanonicalizeReplace(request, payload), request.Signature));
    }

    private static void AssertDifferent(byte[] baseline, ISignedIntent intent, ReplaceRulePayload payload) =>
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeReplace(intent, payload)));

    private static ReplaceRulePayload CreatePayload() => new()
    {
        BaselineFingerprint = VALID_FINGERPRINT,
        TargetOccurrenceId = 2,
        OriginalRuleId = ORIGINAL_RULE_ID,
        ReplacementRule = new FirewallRuleSpecification
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

    private static ReplaceRulePayload Clone(ReplaceRulePayload source, string? baselineFingerprint = null, int? targetOccurrenceId = null, string? originalRuleId = null) => new()
    {
        BaselineFingerprint = baselineFingerprint ?? source.BaselineFingerprint,
        TargetOccurrenceId = targetOccurrenceId ?? source.TargetOccurrenceId,
        OriginalRuleId = originalRuleId ?? source.OriginalRuleId,
        ReplacementRule = new FirewallRuleSpecification
        {
            Action = source.ReplacementRule.Action,
            AddressFamily = source.ReplacementRule.AddressFamily,
            Direction = source.ReplacementRule.Direction,
            Protocol = source.ReplacementRule.Protocol,
            Source = source.ReplacementRule.Source,
            SourcePorts = source.ReplacementRule.SourcePorts,
            SourceInterface = source.ReplacementRule.SourceInterface,
            Destination = source.ReplacementRule.Destination,
            DestinationPorts = source.ReplacementRule.DestinationPorts,
            DestinationInterface = source.ReplacementRule.DestinationInterface,
            Comment = source.ReplacementRule.Comment,
        },
    };

    private static TestIntent CreateIntent() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.REPLACE_RULE,
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
