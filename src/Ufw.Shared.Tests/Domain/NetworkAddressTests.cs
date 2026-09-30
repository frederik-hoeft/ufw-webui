using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class NetworkAddressTests
{
    [TestMethod]
    public void ParseIPv4_MasksToTheNetworkAndKeepsHostAddresses()
    {
        Interval<uint> network = NetworkAddress.ParseIPv4("10.1.2.3/17");

        Assert.AreEqual(NetworkAddress.ParseIPv4("10.1.0.0").Start, network.Start);
        Assert.AreEqual(NetworkAddress.ParseIPv4("10.1.127.255").Start, network.End);
        Assert.IsTrue(network.Contains(NetworkAddress.ParseIPv4("10.1.0.0").Start));
        Assert.IsFalse(network.Contains(NetworkAddress.ParseIPv4("10.1.128.0").Start));
        Assert.AreEqual(NetworkAddress.ParseIPv4("192.0.2.10"), NetworkAddress.ParseIPv4("192.0.2.10/32"));
        Assert.AreEqual(new Interval<uint>(0, uint.MaxValue), NetworkAddress.ParseIPv4("any"));
        Assert.AreEqual(new Interval<uint>(0, uint.MaxValue), NetworkAddress.ParseIPv4("0.0.0.0/0"));
        Assert.AreEqual(NetworkAddress.ParseIPv4("0.0.0.0").Start, NetworkAddress.ParseIPv4("0.0.0.0").End);
    }

    [TestMethod]
    public void ParseIPv6_HonorsNonBytePrefixesAndRejectsScope()
    {
        Interval<UInt128> network = NetworkAddress.ParseIPv6("2001:db8:1:2::1234/64");

        Assert.AreEqual(NetworkAddress.ParseIPv6("2001:db8:1:2::").Start, network.Start);
        Assert.AreEqual(NetworkAddress.ParseIPv6("2001:db8:1:2:ffff:ffff:ffff:ffff").Start, network.End);
        Assert.IsFalse(network.Contains(NetworkAddress.ParseIPv6("2001:db8:1:3::").Start));
        Assert.AreEqual(new Interval<UInt128>(UInt128.Zero, UInt128.MaxValue), NetworkAddress.ParseIPv6("::/0"));
        Assert.ThrowsExactly<FormatException>(() => NetworkAddress.ParseIPv6("fe80::1%3"));
        Assert.ThrowsExactly<FormatException>(() => NetworkAddress.ParseIPv4("2001:db8::1"));
        Assert.ThrowsExactly<FormatException>(() => NetworkAddress.ParseIPv4("10.0.0.0/33"));
    }

    [TestMethod]
    public void ParsePorts_NormalizesRangesAndRejectsZero()
    {
        IntervalSet<ushort> ports = PacketPorts.Parse("80,22,81:90,443");

        Assert.AreEqual(IntervalSet<ushort>.Of([new Interval<ushort>(22, 22), new Interval<ushort>(80, 90), new Interval<ushort>(443, 443)]), ports);
        Assert.AreEqual(PacketPorts.Universe, PacketPorts.Parse(null));
        Assert.AreEqual(PacketPorts.Universe, PacketPorts.Parse("any"));
        Assert.AreEqual(new BigInteger(65535), PacketPorts.Universe.Cardinality);
        Assert.ThrowsExactly<FormatException>(() => PacketPorts.Parse("0"));
        Assert.ThrowsExactly<FormatException>(() => PacketPorts.Parse("80:22"));
        Assert.ThrowsExactly<FormatException>(() => PacketPorts.Parse("65536"));
        Assert.ThrowsExactly<FormatException>(() => PacketPorts.Parse("08"));
    }
}
