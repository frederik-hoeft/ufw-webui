using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class FirewallStateAssessmentSerializationTests
{
    private static readonly int[] s_duplicateOccurrences = [0, 1];

    [TestMethod]
    public void SnapshotRoundtrip_PreservesAmbiguityWithoutChangingSignedFingerprint()
    {
        ListedFirewallRule[] rules =
        [
            new() { RuleId = "sha256:duplicate", Parsed = true },
            new() { RuleId = "sha256:duplicate", Parsed = true },
        ];
        FirewallConfigurationSnapshot configuration = new(true, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Deny);
        RuleListResponse withoutAssessment = new(true, rules, configuration);
        RuleListResponse snapshot = withoutAssessment with { Assessment = FirewallStateAssessmentEvaluator.Evaluate(rules) };

        string json = JsonSerializer.Serialize(snapshot, MessageJsonSerializerContext.Default.RuleListResponse);
        RuleListResponse roundtrip = JsonSerializer.Deserialize(json, MessageJsonSerializerContext.Default.RuleListResponse)!;

        Assert.IsFalse(roundtrip.Assessment.IsClean);
        Assert.AreEqual(FirewallStateAssessmentEvaluator.DUPLICATE_RULE_IDENTITY, roundtrip.Assessment.Issues[0].Code);
        CollectionAssert.AreEqual(s_duplicateOccurrences, roundtrip.Assessment.Issues[0].OccurrenceIds.ToArray());
        Assert.AreEqual(FirewallRuleSnapshotFingerprint.Compute(withoutAssessment), FirewallRuleSnapshotFingerprint.Compute(snapshot));
    }
}
