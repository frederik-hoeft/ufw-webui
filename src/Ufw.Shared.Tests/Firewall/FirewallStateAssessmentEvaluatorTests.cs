using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer static readonly fields over constant array arguments", Justification = "One-shot test comparison.")]
public sealed class FirewallStateAssessmentEvaluatorTests
{
    [TestMethod]
    public void Evaluate_DuplicateSemanticIdentityReportsAllOccurrencesInOrder()
    {
        ListedFirewallRule[] rules =
        [
            new() { RuleId = "sha256:duplicate" },
            new() { RuleId = "sha256:other" },
            new() { RuleId = "sha256:duplicate" },
        ];

        FirewallStateAssessment assessment = FirewallStateAssessmentEvaluator.Evaluate(rules);

        Assert.IsFalse(assessment.IsClean);
        Assert.HasCount(1, assessment.Issues);
        FirewallStateIssue issue = assessment.Issues[0];
        Assert.AreEqual(FirewallStateAssessmentEvaluator.DUPLICATE_RULE_IDENTITY, issue.Code);
        Assert.AreEqual("sha256:duplicate", issue.RuleId);
        CollectionAssert.AreEqual(new[] { 0, 2 }, issue.OccurrenceIds.ToArray());
    }

    [TestMethod]
    public void Evaluate_FamilySpecificIdentitiesAndUnknownRowsAreNotFalseDuplicates()
    {
        ListedFirewallRule[] rules =
        [
            new() { RuleId = "sha256:ipv4" },
            new() { RuleId = "sha256:ipv6" },
            new() { RuleId = null },
            new() { RuleId = null },
        ];

        Assert.IsTrue(FirewallStateAssessmentEvaluator.Evaluate(rules).IsClean);
    }
}
