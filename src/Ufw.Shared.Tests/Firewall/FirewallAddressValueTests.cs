using Ufw.Shared.Firewall;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class FirewallAddressValueTests
{
    [TestMethod]
    [DataRow("192.0.2.129/24", "192.0.2.0/24", FirewallAddressFamily.IPv4)]
    [DataRow("192.0.2.10", "192.0.2.10", FirewallAddressFamily.IPv4)]
    [DataRow("2001:0db8:0:0::1/64", "2001:db8::/64", FirewallAddressFamily.IPv6)]
    public void TryNormalizeLiteral_ValidLiteral_CanonicalizesAddress(
        string input,
        string expected,
        FirewallAddressFamily expectedFamily)
    {
        bool success = FirewallAddressValue.TryNormalizeLiteral(input, out string? normalized, out FirewallAddressFamily family);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, normalized);
        Assert.AreEqual(expectedFamily, family);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("any")]
    [DataRow("Anywhere")]
    [DataRow("0.0.0.0/0")]
    [DataRow("192.0.2.1/99")]
    [DataRow("not-an-address")]
    [DataRow("fe80::1%3")]
    public void TryNormalizeLiteral_NonLiteralOrInvalidAddress_Rejects(string? input)
    {
        bool success = FirewallAddressValue.TryNormalizeLiteral(input, out string? normalized, out FirewallAddressFamily family);

        Assert.IsFalse(success);
        Assert.IsNull(normalized);
        Assert.AreEqual(FirewallAddressFamily.Any, family);
    }
}
