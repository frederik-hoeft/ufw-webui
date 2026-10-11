using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Interop.Output;

[TestClass]
public sealed class UfwVerboseStatusParserTests
{
    [TestMethod]
    public void TryParse_ActiveMatchingRules_ReturnsEffectivePolicies()
    {
        UfwStatusSnapshot numbered = UfwStatusParser.Parse(UfwStatusFixtures.TWO_RULES)!;
        string verbose = UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES)
            .Replace("deny (incoming), allow (outgoing), deny (routed)", "reject (incoming), deny (outgoing), disabled (routed)", StringComparison.Ordinal);

        bool parsed = UfwVerboseStatusParser.TryParse(verbose, numbered, out (FirewallDefaultPolicy Incoming, FirewallDefaultPolicy Outgoing, FirewallDefaultPolicy Routed)? policies);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(policies);
        Assert.AreEqual(FirewallDefaultPolicy.Reject, policies.Value.Incoming);
        Assert.AreEqual(FirewallDefaultPolicy.Deny, policies.Value.Outgoing);
        Assert.AreEqual(FirewallDefaultPolicy.Disabled, policies.Value.Routed);
    }

    [TestMethod]
    public void TryParse_InactiveMatchingStatus_HasNoEffectivePolicies()
    {
        UfwStatusSnapshot numbered = UfwStatusParser.Parse(UfwStatusFixtures.INACTIVE)!;

        Assert.IsTrue(UfwVerboseStatusParser.TryParse("Status: inactive", numbered, out (FirewallDefaultPolicy, FirewallDefaultPolicy, FirewallDefaultPolicy)? policies));
        Assert.IsNull(policies);
    }

    [TestMethod]
    [DataRow("Status: active\nDefault: deny (incoming), allow (outgoing), deny (routed)", DisplayName = "Rules missing")]
    [DataRow("Status: inactive", DisplayName = "Active status disagrees")]
    [DataRow("Status: active\nDefault: accept (incoming), allow (outgoing), deny (routed)\nTo Action From\n-- ------ ----\n22/tcp ALLOW IN Anywhere", DisplayName = "Invalid policy")]
    public void TryParse_RejectsInconsistentOrMalformedOutput(string output)
    {
        UfwStatusSnapshot numbered = UfwStatusParser.Parse(UfwStatusFixtures.TWO_RULES)!;

        Assert.IsFalse(UfwVerboseStatusParser.TryParse(output, numbered, out _));
    }

    [TestMethod]
    public void TryParse_RejectsMovedOrReplacedRules()
    {
        UfwStatusSnapshot numbered = UfwStatusParser.Parse(UfwStatusFixtures.TWO_RULES)!;
        string verbose = UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES);
        string modified = verbose.Replace("22/tcp", "23/tcp", StringComparison.Ordinal);
        string reordered = verbose.Replace("80/tcp", "81/tcp", StringComparison.Ordinal);

        Assert.IsFalse(UfwVerboseStatusParser.TryParse(modified, numbered, out _));
        Assert.IsFalse(UfwVerboseStatusParser.TryParse(reordered, numbered, out _));
    }

    [TestMethod]
    public void TryParse_RejectsMissingDefaultOrDuplicateDefault()
    {
        UfwStatusSnapshot numbered = UfwStatusParser.Parse(UfwStatusFixtures.TWO_RULES)!;
        string verbose = UfwStatusFixtures.VerboseFromNumbered(UfwStatusFixtures.TWO_RULES);
        string defaultLine = "Default: deny (incoming), allow (outgoing), deny (routed)";

        Assert.IsFalse(UfwVerboseStatusParser.TryParse(verbose.Replace(defaultLine, "", StringComparison.Ordinal), numbered, out _));
        Assert.IsFalse(UfwVerboseStatusParser.TryParse(verbose.Replace(defaultLine, defaultLine + "\n" + defaultLine, StringComparison.Ordinal), numbered, out _));
    }
}
