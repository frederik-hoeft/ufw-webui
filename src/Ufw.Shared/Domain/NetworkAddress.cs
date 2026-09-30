using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Parses literal addresses and CIDRs into the numeric ranges the policy model evaluates.
/// Null, blank, <c>any</c>, and <c>anywhere</c> denote the whole family. Prefix 0 does too.
/// A host without a prefix is a single address. This parser does not apply UFW text aliases such as bare <c>0.0.0.0</c>;
/// the firewall projector normalizes those before calling in.
/// </summary>
public static class NetworkAddress
{
    /// <summary>Gets every IPv4 address.</summary>
    public static IntervalSet<uint> IPv4Universe { get; } = IntervalSet<uint>.Between(0, uint.MaxValue);

    /// <summary>Gets every IPv6 address.</summary>
    public static IntervalSet<UInt128> IPv6Universe { get; } = IntervalSet<UInt128>.Between(UInt128.Zero, UInt128.MaxValue);

    /// <summary>Parses an IPv4 host or CIDR. <paramref name="text"/> may be <c>any</c>.</summary>
    public static Interval<uint> ParseIPv4(string? text)
    {
        if (IsAny(text))
        {
            return new Interval<uint>(0, uint.MaxValue);
        }

        ParsedAddress parsed = Parse(text!, AddressFamily.InterNetwork, maxPrefix: 32);
        return IPv4Range(ToUInt32(parsed.Address), parsed.Prefix);
    }

    /// <summary>Parses an IPv6 host or CIDR. <paramref name="text"/> may be <c>any</c>. Scoped addresses are rejected.</summary>
    public static Interval<UInt128> ParseIPv6(string? text)
    {
        if (IsAny(text))
        {
            return new Interval<UInt128>(UInt128.Zero, UInt128.MaxValue);
        }

        ParsedAddress parsed = Parse(text!, AddressFamily.InterNetworkV6, maxPrefix: 128);
        return IPv6Range(ToUInt128(parsed.Address), parsed.Prefix);
    }

    private static bool IsAny(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string trimmed = text.Trim();
        return trimmed.Equals("any", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("anywhere", StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedAddress Parse(string text, AddressFamily family, int maxPrefix)
    {
        string trimmed = text.Trim();
        int slash = trimmed.IndexOf('/');
        string host = slash < 0 ? trimmed : trimmed[..slash];
        if (host.Length == 0 || !IPAddress.TryParse(host, out IPAddress? address) || address.AddressFamily != family)
        {
            throw new FormatException($"'{text}' is not a {FormatFamily(family)} address or CIDR.");
        }

        if (family == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
        {
            throw new FormatException("Scoped IPv6 addresses are not part of the policy model.");
        }

        int prefix = maxPrefix;
        if (slash >= 0 && !TryParsePrefix(trimmed[(slash + 1)..], maxPrefix, out prefix))
        {
            throw new FormatException($"'{text}' has a prefix length outside 0..{maxPrefix}.");
        }

        return new ParsedAddress(address, prefix);
    }

    private static bool TryParsePrefix(string text, int maxPrefix, out int prefix)
    {
        prefix = 0;
        if (text.Length == 0 || (text.Length > 1 && text[0] == '0'))
        {
            return false;
        }

        foreach (char character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out prefix)
            && prefix >= 0
            && prefix <= maxPrefix;
    }

    private static Interval<uint> IPv4Range(uint address, int prefix)
    {
        if (prefix == 0)
        {
            return new Interval<uint>(0, uint.MaxValue);
        }

        if (prefix == 32)
        {
            return new Interval<uint>(address, address);
        }

        uint hostMask = uint.MaxValue >> prefix;
        uint network = address & ~hostMask;
        return new Interval<uint>(network, network | hostMask);
    }

    private static Interval<UInt128> IPv6Range(UInt128 address, int prefix)
    {
        if (prefix == 0)
        {
            return new Interval<UInt128>(UInt128.Zero, UInt128.MaxValue);
        }

        if (prefix == 128)
        {
            return new Interval<UInt128>(address, address);
        }

        int hostBits = 128 - prefix;
        UInt128 hostMask = (UInt128.One << hostBits) - UInt128.One;
        UInt128 network = address & ~hostMask;
        return new Interval<UInt128>(network, network | hostMask);
    }

    private static uint ToUInt32(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (!address.TryWriteBytes(bytes, out int written) || written != 4)
        {
            throw new FormatException("IPv4 address was not four bytes.");
        }

        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static UInt128 ToUInt128(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!address.TryWriteBytes(bytes, out int written) || written != 16)
        {
            throw new FormatException("IPv6 address was not sixteen bytes.");
        }

        return BinaryPrimitives.ReadUInt128BigEndian(bytes);
    }

    private static string FormatFamily(AddressFamily family) => family switch
    {
        AddressFamily.InterNetwork => "IPv4",
        AddressFamily.InterNetworkV6 => "IPv6",
        _ => "IP",
    };

    private readonly record struct ParsedAddress(IPAddress Address, int Prefix);
}
