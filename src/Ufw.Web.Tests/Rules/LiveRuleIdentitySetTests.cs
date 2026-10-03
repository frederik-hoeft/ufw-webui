using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Rules;

[TestClass]
public sealed class LiveRuleIdentitySetTests
{
    [TestMethod]
    public void FromSnapshot_FiltersInvalidValuesDeduplicatesOrdinallyAndEnumeratesDeterministically()
    {
        RuleListResponse snapshot = new(
            Active: true,
            [
                Listed("sha256:b"),
                Listed(null),
                Listed("   "),
                Listed("sha256:a"),
                Listed("sha256:b"),
                Listed("sha256:A"),
            ],
            TestFirewallConfiguration.Enabled);

        LiveRuleIdentitySet identities = LiveRuleIdentitySet.FromSnapshot(snapshot);

        Assert.AreEqual(3, identities.Count);
        Assert.IsTrue(identities.Contains("sha256:a"));
        Assert.IsTrue(identities.Contains("sha256:A"));
        Assert.IsFalse(identities.Contains("SHA256:A"));
        CollectionAssert.AreEqual(new[] { "sha256:A", "sha256:a", "sha256:b" }, identities.ToArray());
    }

    private static ListedFirewallRule Listed(string? ruleId) => new()
    {
        RuleId = ruleId,
        RawLine = ruleId ?? string.Empty,
    };
}
