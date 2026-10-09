using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Semantics;

/// <summary>
/// Adapts shared network/port sets to filter operands and canonical UI text. Parsing, masking, merging and set algebra belong to Ufw.Shared.Domain.
/// </summary>
internal static class RuleFilterSemantics
{
    public static bool TryParseNetwork(string? value, [NotNullWhen(true)] out NetworkFilterOperand? operand)
    {
        operand = null;
        if (string.IsNullOrWhiteSpace(value) || IsAny(value))
        {
            return false;
        }

        string text = value.Trim();
        int slash = text.IndexOf('/');
        ReadOnlySpan<char> address = slash < 0 ? text.AsSpan() : text.AsSpan(0, slash);
        if (!IPAddress.TryParse(address, out IPAddress? parsedAddress))
        {
            return false;
        }

        try
        {
            switch (parsedAddress.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                {
                    Interval<uint> ipv4 = NetworkAddress.ParseIPv4(text);
                    operand = new NetworkFilterOperand(FirewallAddressFamily.IPv4, IntervalSet<uint>.Of(ipv4), default, FormatIPv4(ipv4.Start, text));
                    return true;
                }
                case AddressFamily.InterNetworkV6:
                {
                    Interval<UInt128> ipv6 = NetworkAddress.ParseIPv6(text);
                    operand = new NetworkFilterOperand(FirewallAddressFamily.IPv6, default, IntervalSet<UInt128>.Of(ipv6), FormatIPv6(ipv6.Start, text));
                    return true;
                }
                default:
                    return false;
            }
        }
        catch (FormatException)
        {
            // The shared parser validates the full CIDR syntax and rejects scoped IPv6 addresses.
            return false;
        }
    }

    public static bool TryParseNetwork(string? value, FirewallAddressFamily addressFamily, [NotNullWhen(true)] out NetworkFilterOperand? operand)
    {
        if (TryParseNetwork(value, out NetworkFilterOperand? parsed) && parsed.AddressFamily == addressFamily)
        {
            operand = parsed;
            return true;
        }

        operand = null;
        return false;
    }

    public static NetworkFilterOperand AnyNetwork(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => new(family, NetworkAddress.IPv4Universe, default, "0.0.0.0/0"),
        FirewallAddressFamily.IPv6 => new(family, default, NetworkAddress.IPv6Universe, "::/0"),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "A concrete address family is required."),
    };

    public static bool TryParsePorts(string? value, [NotNullWhen(true)] out PortFilterOperand? operand)
    {
        operand = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            IntervalSet<ushort> ports = PacketPorts.Parse(value);
            operand = new PortFilterOperand(ports, FormatPorts(ports));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string FormatIPv4(uint start, string input)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, start);
        return FormatNetwork(new IPAddress(bytes).ToString(), input, 32);
    }

    private static string FormatIPv6(UInt128 start, string input)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt128BigEndian(bytes, start);
        return FormatNetwork(new IPAddress(bytes).ToString(), input, 128);
    }

    private static string FormatNetwork(string address, string input, int hostPrefix)
    {
        int separator = input.IndexOf('/');
        if (separator < 0)
        {
            return address;
        }

        // Input syntax was already validated by NetworkAddress; this is only UI formatting.
        int prefix = int.Parse(input.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture);
        return prefix == hostPrefix ? address : $"{address}/{prefix.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatPorts(IntervalSet<ushort> ports) => string.Join(',', ports.Intervals.Select(static range => range.Start == range.End
        ? range.Start.ToString(CultureInfo.InvariantCulture)
        : $"{range.Start.ToString(CultureInfo.InvariantCulture)}:{range.End.ToString(CultureInfo.InvariantCulture)}"));

    private static bool IsAny(string value)
    {
        string text = value.Trim();
        return text.Equals("any", StringComparison.OrdinalIgnoreCase) || text.Equals("anywhere", StringComparison.OrdinalIgnoreCase);
    }
}
