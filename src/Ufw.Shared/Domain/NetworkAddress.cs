using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Extensions;

namespace Ufw.Shared.Domain;

/// <summary>
/// Parses literal addresses and CIDRs into the numeric ranges the policy model evaluates.
/// Null, blank, <c>any</c>, and <c>anywhere</c> denote the whole family. Prefix 0 does too.
/// A host without a prefix is a single address. This parser does not apply UFW text aliases such as bare <c>0.0.0.0</c>;
/// the firewall projector normalizes those before calling in.
/// </summary>
public static class NetworkAddress
{
    /// <summary>
    /// Gets every IPv4 address.
    /// </summary>
    public static IntervalSet<uint> IPv4Universe { get; } = IntervalSet<uint>.Between(0, uint.MaxValue);

    /// <summary>
    /// Gets every IPv6 address.
    /// </summary>
    public static IntervalSet<UInt128> IPv6Universe { get; } = IntervalSet<UInt128>.Between(UInt128.Zero, UInt128.MaxValue);

    /// <summary>
    /// Parses an IPv4 host or CIDR. <paramref name="text"/> may be <c>any</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="text"/> is not a valid IPv4 address or CIDR.</exception>
    public static Interval<uint> ParseIPv4(string? text) => ParseIPv4(text.AsSpan());

    /// <summary>
    /// Parses an IPv4 host or CIDR. <paramref name="text"/> may be <c>any</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="text"/> is not a valid IPv4 address or CIDR.</exception>
    public static Interval<uint> ParseIPv4(ReadOnlySpan<char> text) => TryParseIPv4Core(text, out Interval<uint> range, out ParseError error)
        ? range
        : ThrowParseError<uint>(text, AddressFamily.InterNetwork, error);

    /// <summary>
    /// Parses an IPv6 host or CIDR. <paramref name="text"/> may be <c>any</c>. Scoped addresses are rejected.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="text"/> is not a valid IPv6 address or CIDR.</exception>
    public static Interval<UInt128> ParseIPv6(string? text) => ParseIPv6(text.AsSpan());

    /// <summary>
    /// Parses an IPv6 host or CIDR. <paramref name="text"/> may be <c>any</c>. Scoped addresses are rejected.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="FormatException">Thrown if <paramref name="text"/> is not a valid IPv6 address or CIDR.</exception>
    public static Interval<UInt128> ParseIPv6(ReadOnlySpan<char> text) => TryParseIPv6Core(text, out Interval<UInt128> range, out ParseError error)
        ? range
        : ThrowParseError<UInt128>(text, AddressFamily.InterNetworkV6, error);

    /// <summary>
    /// Parses an IPv4 host or CIDR. <paramref name="text"/> may be <c>any</c>. Returns false if the text is not a valid IPv4 address or CIDR.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="range">The parsed range, if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseIPv4(ReadOnlySpan<char> text, out Interval<uint> range) =>
        TryParseIPv4Core(text, out range, out _);

    /// <summary>
    /// Parses an IPv4 host or CIDR. <paramref name="text"/> may be <c>any</c>. Returns false if the text is not a valid IPv4 address or CIDR.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="range">The parsed range, if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseIPv4(string? text, out Interval<uint> range) =>
        TryParseIPv4Core(text.AsSpan(), out range, out _);

    /// <summary>
    /// Parses an IPv6 host or CIDR. <paramref name="text"/> may be <c>any</c>. Scoped addresses are rejected. Returns false if the text is not a valid IPv6 address or CIDR.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="range">The parsed range, if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseIPv6(ReadOnlySpan<char> text, out Interval<UInt128> range) =>
        TryParseIPv6Core(text, out range, out _);

    /// <summary>
    /// Parses an IPv6 host or CIDR. <paramref name="text"/> may be <c>any</c>. Scoped addresses are rejected. Returns false if the text is not a valid IPv6 address or CIDR.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="range">The parsed range, if successful.</param>
    /// <returns><see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseIPv6(string? text, out Interval<UInt128> range) =>
        TryParseIPv6Core(text.AsSpan(), out range, out _);

    private static bool TryParseIPv4Core(ReadOnlySpan<char> text, out Interval<uint> range, out ParseError error)
    {
        text = text.Trim();
        if (IsAny(text))
        {
            range = new Interval<uint>(0, uint.MaxValue);
            return true.WithDefaults(out error);
        }
        if (TryParse(text, AddressFamily.InterNetwork, maxPrefix: 32, out ParsedAddress parsed, out error)
            && TryCastToUInt32(parsed.Address, out uint address, out error))
        {
            range = IPv4Range(address, parsed.Prefix);
            return true;
        }
        return false.WithDefaults(out range);
    }

    private static bool TryParseIPv6Core(ReadOnlySpan<char> text, out Interval<UInt128> range, out ParseError error)
    {
        text = text.Trim();
        if (IsAny(text))
        {
            range = new Interval<UInt128>(UInt128.Zero, UInt128.MaxValue);
            return true.WithDefaults(out error);
        }
        if (TryParse(text, AddressFamily.InterNetworkV6, maxPrefix: 128, out ParsedAddress parsed, out error)
            && TryCastToUInt128(parsed.Address, out UInt128 address, out error))
        {
            range = IPv6Range(address, parsed.Prefix);
            return true;
        }
        return false.WithDefaults(out range);
    }

    private static Interval<T> ThrowParseError<T>(ReadOnlySpan<char> text, AddressFamily family, ParseError error)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
        => error switch
        {
            ParseError.None => throw new FormatException($"'{text}' is not a valid {FormatFamily(family)} address or CIDR."),
            ParseError.InvalidAddress => throw new FormatException($"'{text}' is not a valid {FormatFamily(family)} address."),
            ParseError.InvalidFamily => throw new FormatException($"'{text}' is not a {FormatFamily(family)} address."),
            ParseError.InvalidPrefix => throw new FormatException($"'{text}' has a prefix length outside 0..{(family == AddressFamily.InterNetwork ? 32 : 128)}."),
            ParseError.UnsupportedIpv6Scoped => throw new FormatException($"'{text}' is a scoped IPv6 address, which is not part of the policy model."),
            _ => throw new FormatException($"'{text}' is not a valid {FormatFamily(family)} address or CIDR: {error}.")
        };

    private static bool IsAny(ReadOnlySpan<char> text) =>
        text.IsWhiteSpace()
        || text.Equals("any", StringComparison.OrdinalIgnoreCase)
        || text.Equals("anywhere", StringComparison.OrdinalIgnoreCase);

    private static bool TryParse(ReadOnlySpan<char> input, AddressFamily family, int maxPrefix, out ParsedAddress parsedAddress, out ParseError error)
    {
        int slash = input.IndexOf('/', out int hostBoundary);
        ReadOnlySpan<char> host = input[..hostBoundary];
        if (host.IsEmpty || !IPAddress.TryParse(host, out IPAddress? address))
        {
            error = ParseError.InvalidAddress;
            return false.WithDefaults(out parsedAddress);
        }
        if (address.AddressFamily != family)
        {
            error = ParseError.InvalidFamily;
            return false.WithDefaults(out parsedAddress);
        }
        if (family == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
        {
            error = ParseError.UnsupportedIpv6Scoped;
            return false.WithDefaults(out parsedAddress);
        }
        int prefix = maxPrefix;
        if (slash != -1 && !TryParsePrefix(input[(slash + 1)..], maxPrefix, out prefix))
        {
            error = ParseError.InvalidPrefix;
            return false.WithDefaults(out parsedAddress);
        }
        parsedAddress = new ParsedAddress(address, prefix);
        return true.WithDefaults(out error);
    }

    private static bool TryParsePrefix(ReadOnlySpan<char> text, int maxPrefix, out int prefix)
    {
        if (text.IsEmpty || text is ['0', _, ..])
        {
            return false.WithDefaults(out prefix);
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out prefix)
            && prefix >= 0 && prefix <= maxPrefix;
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

    private static bool TryCastToUInt32(IPAddress address, out uint value, out ParseError error)
    {
        const int IPV4_BYTE_COUNT = 4;
        Span<byte> bytes = stackalloc byte[IPV4_BYTE_COUNT];
        if (address.AddressFamily != AddressFamily.InterNetwork || !address.TryWriteBytes(bytes, out int written) || written != IPV4_BYTE_COUNT)
        {
            error = ParseError.InvalidAddress;
            return false.WithDefaults(out value);
        }
        value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return true.WithDefaults(out error);
    }

    private static bool TryCastToUInt128(IPAddress address, out UInt128 value, out ParseError error)
    {
        const int IPV6_BYTE_COUNT = 16;
        Span<byte> bytes = stackalloc byte[IPV6_BYTE_COUNT];
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || !address.TryWriteBytes(bytes, out int written) || written != IPV6_BYTE_COUNT)
        {
            error = ParseError.InvalidAddress;
            return false.WithDefaults(out value);
        }
        value = BinaryPrimitives.ReadUInt128BigEndian(bytes);
        return true.WithDefaults(out error);
    }

    private static string FormatFamily(AddressFamily family) => family switch
    {
        AddressFamily.InterNetwork => "IPv4",
        AddressFamily.InterNetworkV6 => "IPv6",
        _ => "IP",
    };

    private readonly record struct ParsedAddress(IPAddress Address, int Prefix);

    private enum ParseError
    {
        None,
        InvalidAddress,
        InvalidFamily,
        InvalidPrefix,
        UnsupportedIpv6Scoped
    }
}
