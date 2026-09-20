using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Networks;

internal sealed record RuleNetwork
{
    private RuleNetwork(IPAddress networkAddress, int prefixLength, FirewallAddressFamily addressFamily)
    {
        NetworkAddress = networkAddress;
        PrefixLength = prefixLength;
        AddressFamily = addressFamily;
    }

    public IPAddress NetworkAddress { get; }

    public int PrefixLength { get; }

    public FirewallAddressFamily AddressFamily { get; }

    public string CanonicalValue
    {
        get
        {
            int hostPrefix = AddressFamily == FirewallAddressFamily.IPv4 ? 32 : 128;
            return PrefixLength == hostPrefix ? NetworkAddress.ToString() : $"{NetworkAddress}/{PrefixLength.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out RuleNetwork? network)
    {
        network = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        int slash = trimmed.IndexOf('/', StringComparison.Ordinal);
        string host = slash < 0 ? trimmed : trimmed[..slash];
        if (!IPAddress.TryParse(host, out IPAddress? address) || (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && address.ScopeId != 0))
        {
            return false;
        }

        FirewallAddressFamily family;
        int hostPrefix;
        switch (address.AddressFamily)
        {
            case System.Net.Sockets.AddressFamily.InterNetwork:
                family = FirewallAddressFamily.IPv4;
                hostPrefix = 32;
                break;
            case System.Net.Sockets.AddressFamily.InterNetworkV6:
                family = FirewallAddressFamily.IPv6;
                hostPrefix = 128;
                break;
            default:
                return false;
        }

        int prefixLength = hostPrefix;
        if (slash >= 0 && (!int.TryParse(trimmed.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out prefixLength)
            || prefixLength < 0 || prefixLength > hostPrefix))
        {
            return false;
        }

        network = new RuleNetwork(ApplyPrefix(address, prefixLength), prefixLength, family);
        return true;
    }

    public static RuleNetwork Any(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => new RuleNetwork(IPAddress.Any, 0, family),
        FirewallAddressFamily.IPv6 => new RuleNetwork(IPAddress.IPv6Any, 0, family),
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "A concrete address family is required."),
    };

    public bool Contains(RuleNetwork other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (AddressFamily != other.AddressFamily || PrefixLength > other.PrefixLength)
        {
            return false;
        }

        byte[] left = NetworkAddress.GetAddressBytes();
        byte[] right = other.NetworkAddress.GetAddressBytes();
        int fullBytes = PrefixLength / 8;
        int remainingBits = PrefixLength % 8;
        for (int index = 0; index < fullBytes; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        byte mask = (byte)(0xff << (8 - remainingBits));
        return (left[fullBytes] & mask) == (right[fullBytes] & mask);
    }

    private static IPAddress ApplyPrefix(IPAddress address, int prefixLength)
    {
        byte[] bytes = address.GetAddressBytes();
        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;
        if (remainingBits != 0 && fullBytes < bytes.Length)
        {
            bytes[fullBytes] &= (byte)(0xff << (8 - remainingBits));
            fullBytes++;
        }
        for (int index = fullBytes; index < bytes.Length; index++)
        {
            bytes[index] = 0;
        }
        return new IPAddress(bytes);
    }
}
