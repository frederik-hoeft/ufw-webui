using System.Text;
using System.Text.Json;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class BatchDeleteIntentCanonicalizerTests
{
    [TestMethod]
    public void CanonicalizeBatchDelete_BindsBaselineAndEveryOccurrence()
    {
        TestIntent intent = CreateIntent();
        BatchDeleteRulesPayload payload = new()
        {
            BaselineFingerprint = "sha256:baseline",
            OccurrenceIds = [5, 2, 0],
        };

        string canonical = Encoding.UTF8.GetString(IntentCanonicalizer.CanonicalizeBatchDelete(intent, payload));

        Assert.AreEqual(
            "ufw-intent/2\n"
            + "deploymentId=deployment\n"
            + "keyId=key\n"
            + "issuedAtUnix=123\n"
            + "nonce=nonce\n"
            + "operation=rules.delete-batch\n"
            + "payload:\n"
            + "baselineFingerprint=sha256:baseline\n"
            + "occurrenceIdCount=3\n"
            + "occurrenceIds[0]=5\n"
            + "occurrenceIds[1]=2\n"
            + "occurrenceIds[2]=0\n",
            canonical);
    }

    [TestMethod]
    public void CanonicalizeBatchDelete_AnySignedFieldOrPayloadChange_ChangesBytes()
    {
        TestIntent intent = CreateIntent();
        BatchDeleteRulesPayload payload = new() { BaselineFingerprint = "sha256:baseline", OccurrenceIds = [1, 3] };
        byte[] baseline = IntentCanonicalizer.CanonicalizeBatchDelete(intent, payload);

        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeBatchDelete(intent with { Nonce = "different" }, payload)));
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeBatchDelete(
            intent,
            new BatchDeleteRulesPayload { BaselineFingerprint = "sha256:different", OccurrenceIds = payload.OccurrenceIds })));
        Assert.IsFalse(baseline.SequenceEqual(IntentCanonicalizer.CanonicalizeBatchDelete(
            intent,
            new BatchDeleteRulesPayload { BaselineFingerprint = payload.BaselineFingerprint, OccurrenceIds = [3, 1] })));
    }

    [TestMethod]
    public void BatchDeletePayload_SourceGeneratedJsonContext_RoundTrips()
    {
        BatchDeleteRulesPayload payload = new() { BaselineFingerprint = "sha256:baseline", OccurrenceIds = [2, 0] };

        string json = JsonSerializer.Serialize(payload, MessageJsonSerializerContext.Default.BatchDeleteRulesPayload);
        BatchDeleteRulesPayload? roundTrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.BatchDeleteRulesPayload);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.BaselineFingerprint, roundTrip.BaselineFingerprint);
        CollectionAssert.AreEqual(payload.OccurrenceIds, roundTrip.OccurrenceIds);
    }

    private static TestIntent CreateIntent() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 123,
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULES_BATCH,
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
