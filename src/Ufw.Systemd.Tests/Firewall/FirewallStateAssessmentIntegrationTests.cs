using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer static readonly fields over constant array arguments", Justification = "One-shot test comparison.")]
public sealed class FirewallStateAssessmentIntegrationTests
{
    [TestMethod]
    public void ListedSnapshot_DifferentCommentsSameSemanticIdentity_IsAmbiguous()
    {
        string status = UfwStatusFixtures.WithRules(
        [
            "[ 1] 22/tcp                     ALLOW IN    Anywhere # first",
            "[ 2] 80/tcp                     ALLOW IN    Anywhere",
            "[ 3] 22/tcp                     ALLOW IN    Anywhere # second",
        ]);
        RuleListResponse snapshot = FirewallRuleSet.ToListResponse(UfwStatusParser.Parse(status)!, TestFirewallConfiguration.Enabled);

        Assert.IsFalse(snapshot.Assessment.IsClean);
        Assert.HasCount(1, snapshot.Assessment.Issues);
        FirewallStateIssue issue = snapshot.Assessment.Issues[0];
        Assert.AreEqual(FirewallStateAssessmentEvaluator.DUPLICATE_RULE_IDENTITY, issue.Code);
        CollectionAssert.AreEqual(new[] { 0, 2 }, issue.OccurrenceIds.ToArray());
    }
}
