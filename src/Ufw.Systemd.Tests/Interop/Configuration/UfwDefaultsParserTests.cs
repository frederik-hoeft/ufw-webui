using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Configuration;

namespace Ufw.Systemd.Tests.Interop.Configuration;

[TestClass]
public sealed class UfwDefaultsParserTests
{
    [TestMethod]
    public void TryParse_StandardDefaults_ReturnsAuthoritativeConfiguration()
    {
        const string defaults = """
            # /etc/default/ufw
            IPV6=yes
            DEFAULT_INPUT_POLICY="DROP"
            DEFAULT_OUTPUT_POLICY="ACCEPT"
            DEFAULT_FORWARD_POLICY="REJECT"
            """;

        bool parsed = UfwDefaultsParser.TryParse(defaults, out FirewallConfigurationSnapshot? snapshot);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot.IPv6Enabled);
        Assert.AreEqual(FirewallDefaultPolicy.Deny, snapshot.IncomingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Allow, snapshot.OutgoingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Reject, snapshot.RoutedPolicy);
    }

    [TestMethod]
    public void TryParse_DisabledIpv6AndAliases_AreAccepted()
    {
        const string defaults = """
            IPV6 = no # intentionally disabled
            DEFAULT_INPUT_POLICY = deny
            DEFAULT_OUTPUT_POLICY = 'allow' # comment
            DEFAULT_FORWARD_POLICY = reject
            """;

        bool parsed = UfwDefaultsParser.TryParse(defaults, out FirewallConfigurationSnapshot? snapshot);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(snapshot);
        Assert.IsFalse(snapshot.IPv6Enabled);
        Assert.AreEqual(FirewallDefaultPolicy.Deny, snapshot.IncomingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Allow, snapshot.OutgoingPolicy);
        Assert.AreEqual(FirewallDefaultPolicy.Reject, snapshot.RoutedPolicy);
    }

    [TestMethod]
    [DataRow("IPV6=maybe\nDEFAULT_INPUT_POLICY=DROP\nDEFAULT_OUTPUT_POLICY=ACCEPT\nDEFAULT_FORWARD_POLICY=DROP")]
    [DataRow("IPV6=yes\nDEFAULT_INPUT_POLICY=DROP\nDEFAULT_OUTPUT_POLICY=ACCEPT")]
    [DataRow("IPV6=yes\nDEFAULT_INPUT_POLICY=unsupported\nDEFAULT_OUTPUT_POLICY=ACCEPT\nDEFAULT_FORWARD_POLICY=DROP")]
    [DataRow("IPV6='yes' trailing\nDEFAULT_INPUT_POLICY=DROP\nDEFAULT_OUTPUT_POLICY=ACCEPT\nDEFAULT_FORWARD_POLICY=DROP")]
    public void TryParse_MalformedOrIncompleteDefaults_FailsClosed(string defaults)
    {
        Assert.IsFalse(UfwDefaultsParser.TryParse(defaults, out FirewallConfigurationSnapshot? snapshot));
        Assert.IsNull(snapshot);
    }
}
