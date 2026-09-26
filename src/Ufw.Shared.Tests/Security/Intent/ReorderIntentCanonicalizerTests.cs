using System.Text;
using System.Text.Json;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class ReorderIntentCanonicalizerTests
{
    [TestMethod]
    public void CanonicalizeReorder_BindsBaselineAndCompletePermutation()
    {
        TestIntent intent = CreateIntent();
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = "sha256:baseline",
            DesiredOrder = [0, 3, 1, 2],
        };

        string canonical = Encoding.UTF8.GetString(IntentCanonicalizer.CanonicalizeReorder(intent, payload));

        Assert.AreEqual(
            "ufw-intent/2\n"
            + "deploymentId=deployment\n"
            + "keyId=key\n"
            + "issuedAtUnix=123\n"
            + "nonce=nonce\n"
            + "operation=rules.reorder\n"
            + "payload:\n"
            + "baselineFingerprint=sha256:baseline\n"
            + "desiredOrderCount=4\n"
            + "desiredOrder[0]=0\n"
            + "desiredOrder[1]=3\n"
            + "desiredOrder[2]=1\n"
            + "desiredOrder[3]=2\n",
            canonical);
    }

    [TestMethod]
    public void CanonicalizeReorder_AnySignedFieldOrPayloadChange_ChangesBytes()
    {
        TestIntent intent = CreateIntent();
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = "sha256:baseline",
            DesiredOrder = [0, 2, 1],
        };
        byte[] baseline = IntentCanonicalizer.CanonicalizeReorder(intent, payload);

        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeReorder(
            intent with { Nonce = "different" }, payload)));
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeReorder(
            intent, new ReorderRulesPayload { BaselineFingerprint = "sha256:different", DesiredOrder = payload.DesiredOrder })));
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeReorder(
            intent, new ReorderRulesPayload { BaselineFingerprint = payload.BaselineFingerprint, DesiredOrder = [0, 1, 2] })));
    }

    [TestMethod]
    public void ReorderPayload_SourceGeneratedJsonContext_RoundTrips()
    {
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = "sha256:baseline",
            DesiredOrder = [2, 0, 1],
        };

        string json = JsonSerializer.Serialize(payload, MessageJsonSerializerContext.Default.ReorderRulesPayload);
        ReorderRulesPayload? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.ReorderRulesPayload);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.BaselineFingerprint, roundTrip.BaselineFingerprint);
        CollectionAssert.AreEqual(payload.DesiredOrder, roundTrip.DesiredOrder);
    }

    [TestMethod]
    public void CanonicalizeReorder_RejectsNullDesiredOrder()
    {
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = "sha256:baseline",
            DesiredOrder = null!,
        };

        Assert.Throws<ArgumentNullException>(() => IntentCanonicalizer.CanonicalizeReorder(CreateIntent(), payload));
    }

    private static TestIntent CreateIntent() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.REORDER_RULES,
        Payload = JsonSerializer.SerializeToElement(new { }),
        Signature = "signature",
    };

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
