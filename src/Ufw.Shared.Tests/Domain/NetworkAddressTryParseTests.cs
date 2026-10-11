using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class NetworkAddressTryParseTests
{
    [TestMethod]
    public void TryParseIPv4_AcceptsHostPrefixesAndAnyAliasesAcrossOverloads()
    {
        string?[] valid = [null, "", "   ", "any", "ANYWHERE", " any ", "192.0.2.1", "192.0.2.1/32", "10.1.2.3/17", "0.0.0.0/0", " 10.1.2.3/17 "];
        foreach (string? text in valid)
        {
            Assert.IsTrue(NetworkAddress.TryParseIPv4(text, out Interval<uint> fromString), $"String parse rejected '{text}'.");
            Assert.IsTrue(NetworkAddress.TryParseIPv4(text.AsSpan(), out Interval<uint> fromSpan), $"Span parse rejected '{text}'.");
            Assert.AreEqual(fromString, fromSpan);
            Assert.AreEqual(NetworkAddress.ParseIPv4(text), fromString);
        }

        Assert.AreEqual(NetworkAddress.IPv4Universe.Intervals[0], NetworkAddress.ParseIPv4("0.0.0.0/0"));
        Assert.AreEqual(NetworkAddress.ParseIPv4("10.1.0.0").Start, NetworkAddress.ParseIPv4("10.1.2.3/17").Start);
    }

    [TestMethod]
    public void TryParseIPv6_AcceptsHostPrefixesAndAnyAliasesAcrossOverloads()
    {
        string?[] valid = [null, "", "   ", "any", "anywhere", " ANY ", "2001:db8::1", "2001:db8::1/128", "2001:db8::123/64", "::/0", " 2001:db8::1/127 "];
        foreach (string? text in valid)
        {
            Assert.IsTrue(NetworkAddress.TryParseIPv6(text, out Interval<UInt128> fromString), $"String parse rejected '{text}'.");
            Assert.IsTrue(NetworkAddress.TryParseIPv6(text.AsSpan(), out Interval<UInt128> fromSpan), $"Span parse rejected '{text}'.");
            Assert.AreEqual(fromString, fromSpan);
            Assert.AreEqual(NetworkAddress.ParseIPv6(text), fromString);
        }

        Assert.AreEqual(NetworkAddress.IPv6Universe.Intervals[0], NetworkAddress.ParseIPv6("::/0"));
    }

    [TestMethod]
    public void TryParseIPv4_ReturnsFalseWithoutExceptionsForMalformedOrWrongFamilyInputs()
    {
        string[] invalid = ["bad", "2001:db8::1", "192.0.2.1/", "192.0.2.1/33", "192.0.2.1/00", "192.0.2.1/032", "192.0.2.1/-1", "192.0.2.1/abc", "192.0.2.1/1/2", "192.0.2.1/32x"];
        foreach (string text in invalid)
        {
            Assert.IsFalse(NetworkAddress.TryParseIPv4(text, out Interval<uint> fromString), $"String parse accepted '{text}'.");
            Assert.IsFalse(NetworkAddress.TryParseIPv4(text.AsSpan(), out Interval<uint> fromSpan), $"Span parse accepted '{text}'.");
            Assert.AreEqual(default, fromString);
            Assert.AreEqual(default, fromSpan);
        }
    }

    [TestMethod]
    public void TryParseIPv6_ReturnsFalseWithoutExceptionsForMalformedScopedOrWrongFamilyInputs()
    {
        string[] invalid = ["bad", "192.0.2.1", "fe80::1%3", "2001:db8::1/", "2001:db8::1/129", "2001:db8::1/00", "2001:db8::1/0128", "2001:db8::1/-1", "2001:db8::1/abc", "2001:db8::1/64/2"];
        foreach (string text in invalid)
        {
            Assert.IsFalse(NetworkAddress.TryParseIPv6(text, out Interval<UInt128> fromString), $"String parse accepted '{text}'.");
            Assert.IsFalse(NetworkAddress.TryParseIPv6(text.AsSpan(), out Interval<UInt128> fromSpan), $"Span parse accepted '{text}'.");
            Assert.AreEqual(default, fromString);
            Assert.AreEqual(default, fromSpan);
        }
    }
}
