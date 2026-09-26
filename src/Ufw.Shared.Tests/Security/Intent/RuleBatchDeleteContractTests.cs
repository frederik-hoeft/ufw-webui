using Ufw.Shared.Firewall;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Tests.Security.Intent;

[TestClass]
public sealed class RuleBatchDeleteContractTests
{
    private static readonly string s_validFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: false, []);

    [TestMethod]
    public void ValidatePayload_ValidSelection_Accepts()
    {
        RuleBatchDeleteContract.ValidatePayload(new BatchDeleteRulesPayload { BaselineFingerprint = s_validFingerprint, OccurrenceIds = [0, 2, 5] });
    }

    [TestMethod]
    public void ValidatePayload_EmptySelection_Rejects() =>
        Assert.Throws<ArgumentException>(() => RuleBatchDeleteContract.ValidatePayload(new BatchDeleteRulesPayload { BaselineFingerprint = s_validFingerprint, OccurrenceIds = [] }));

    [TestMethod]
    public void ValidatePayload_DuplicateOccurrence_Rejects() =>
        Assert.Throws<ArgumentException>(() => RuleBatchDeleteContract.ValidatePayload(new BatchDeleteRulesPayload { BaselineFingerprint = s_validFingerprint, OccurrenceIds = [1, 1] }));

    [TestMethod]
    public void ValidatePayload_NegativeOccurrence_Rejects() =>
        Assert.Throws<ArgumentException>(() => RuleBatchDeleteContract.ValidatePayload(new BatchDeleteRulesPayload { BaselineFingerprint = s_validFingerprint, OccurrenceIds = [-1] }));

    [TestMethod]
    public void ValidatePayload_MalformedFingerprint_Rejects() =>
        Assert.Throws<ArgumentException>(() => RuleBatchDeleteContract.ValidatePayload(new BatchDeleteRulesPayload { BaselineFingerprint = "sha256:not-valid", OccurrenceIds = [0] }));
}
