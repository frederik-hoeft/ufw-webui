using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PacketPortsParsingTests
{
    [TestMethod]
    public void Parse_MultiSegmentWhitespaceAndRanges_NormalizesWithoutDroppingEntries()
    {
        IntervalSet<ushort> expected = IntervalSet<ushort>.Of(
        [
            new Interval<ushort>(22, 22),
            new Interval<ushort>(80, 82),
            new Interval<ushort>(443, 443),
        ]);
        string[] inputs =
        [
            "22,80:81,82,443",
            " 22, 80 : 81 , 82,  443 ",
            "22 ,80:81, 82 ,443",
        ];

        foreach (string input in inputs)
        {
            Assert.AreEqual(expected, PacketPorts.Parse(input));
            Assert.AreEqual(expected, PacketPorts.Parse(input.AsSpan()));
            Assert.IsTrue(PacketPorts.TryParse(input, out IntervalSet<ushort> parsed));
            Assert.AreEqual(expected, parsed);
            Assert.IsTrue(PacketPorts.TryParse(input.AsSpan(), out IntervalSet<ushort> parsedSpan));
            Assert.AreEqual(expected, parsedSpan);
        }
    }

    [TestMethod]
    public void TryParse_AcceptsSinglePortsAndWhitespaceAroundRangeDelimiters()
    {
        Assert.IsTrue(PacketPorts.TryParse(" 80 : 443 ", out IntervalSet<ushort> parsed));
        Assert.AreEqual(IntervalSet<ushort>.Of(new Interval<ushort>(80, 443)), parsed);
        Assert.IsTrue(PacketPorts.TryParse("65535", out IntervalSet<ushort> maximum));
        Assert.AreEqual(IntervalSet<ushort>.Of(new Interval<ushort>(65535, 65535)), maximum);
    }

    [TestMethod]
    public void Parse_BlankAndAnyRepresentTheNumericPortUniverse()
    {
        foreach (string? input in new string?[] { null, "", "  ", "any", " ANY " })
        {
            Assert.AreEqual(PacketPorts.Universe, PacketPorts.Parse(input));
            Assert.IsTrue(PacketPorts.TryParse(input, out IntervalSet<ushort> parsed));
            Assert.AreEqual(PacketPorts.Universe, parsed);
        }
    }

    [TestMethod]
    public void TryParse_RejectsEmptySegmentsAndMalformedValuesWithoutThrowing()
    {
        string[] inputs =
        [
            ",80", "80,", "80,,443", "80, ,443", "80,  ", ",", ",,", "80:443,",
            "80:0", "80:65536", "80:70", "08", "0", "65536", "+80", "80:90:100", "any,80",
        ];
        foreach (string input in inputs)
        {
            Assert.IsFalse(PacketPorts.TryParse(input, out _), $"Unexpectedly accepted '{input}' as a port set.");
            Assert.IsFalse(PacketPorts.TryParse(input.AsSpan(), out _), $"Unexpectedly accepted '{input}' as a port set span.");
            Assert.ThrowsExactly<FormatException>(() => PacketPorts.Parse(input));
        }
    }
}
