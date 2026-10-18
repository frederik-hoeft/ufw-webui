using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>Canonicalizes understood iptables predicate values without interpreting module-specific filters.</summary>
internal static class IptablesPredicateNormalizer
{
    public static bool TryCanonicalProtocol(string text, out string? protocol)
    {
        string candidate = text.ToLowerInvariant();
        if (candidate is "all" or "0")
        {
            protocol = null;
            return true;
        }

        candidate = candidate switch
        {
            "6" => "tcp",
            "17" => "udp",
            "1" => "icmp",
            "58" => "ipv6-icmp",
            _ => candidate,
        };

        if (candidate.Length == 0)
        {
            protocol = null;
            return false;
        }

        foreach (char character in candidate)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
            {
                protocol = null;
                return false;
            }
        }

        protocol = candidate;
        return true;
    }

    public static bool TryCanonicalAddress(string text, FirewallAddressFamily family, out string canonical)
    {
        string candidate = text.Trim();
        int slash = candidate.IndexOf('/');
        if (slash > 0 && family == FirewallAddressFamily.IPv4 && candidate[(slash + 1)..].Contains('.', StringComparison.Ordinal))
        {
            if (!TryPrefixFromDottedMask(candidate[(slash + 1)..], out int prefix))
            {
                canonical = string.Empty;
                return false;
            }

            candidate = prefix == 32
                ? candidate[..slash]
                : string.Concat(candidate.AsSpan(0, slash), "/", prefix.ToString(CultureInfo.InvariantCulture));
        }

        int hostSlash = candidate.IndexOf('/');
        string host = hostSlash < 0 ? candidate : candidate[..hostSlash];
        if (!IPAddress.TryParse(host, out IPAddress? parsed)
            || parsed.AddressFamily == AddressFamily.InterNetworkV6 && parsed.ScopeId != 0)
        {
            canonical = string.Empty;
            return false;
        }

        FirewallAddressFamily parsedFamily = parsed.AddressFamily == AddressFamily.InterNetwork
            ? FirewallAddressFamily.IPv4
            : parsed.AddressFamily == AddressFamily.InterNetworkV6
                ? FirewallAddressFamily.IPv6
                : FirewallAddressFamily.Any;
        if (parsedFamily != family)
        {
            canonical = string.Empty;
            return false;
        }

        string normalized = RuleSpecificationNormalizer.NormalizeAddress(candidate);
        if (normalized == RuleSpecificationNormalizer.ANY)
        {
            canonical = normalized;
            return true;
        }

        if (RuleSpecificationNormalizer.GetAddressFamily(normalized) != family)
        {
            canonical = string.Empty;
            return false;
        }

        canonical = normalized;
        return true;
    }

    private static bool TryPrefixFromDottedMask(string mask, out int prefix)
    {
        prefix = 0;
        if (!IPAddress.TryParse(mask, out IPAddress? parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        bool zeroSeen = false;
        foreach (byte value in parsed.GetAddressBytes())
        {
            for (int bit = 7; bit >= 0; bit--)
            {
                bool set = (value & (1 << bit)) != 0;
                if (set)
                {
                    if (zeroSeen)
                    {
                        prefix = 0;
                        return false;
                    }

                    prefix++;
                }
                else
                {
                    zeroSeen = true;
                }
            }
        }

        return true;
    }

    public static bool TryCanonicalPorts(string text, out string ports)
    {
        string? normalized = RuleSpecificationNormalizer.NormalizePorts(text);
        if (string.IsNullOrEmpty(normalized))
        {
            ports = string.Empty;
            return false;
        }

        foreach (string part in normalized.Split(','))
        {
            int colon = part.IndexOf(':');
            if (colon < 0)
            {
                if (!IsPort(part))
                {
                    ports = string.Empty;
                    return false;
                }

                continue;
            }

            if (!IsPort(part[..colon]) || !IsPort(part[(colon + 1)..]))
            {
                ports = string.Empty;
                return false;
            }
        }

        ports = normalized;
        return true;
    }

    private static bool IsPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is >= 1 and <= 65535;

    public static bool IsInterfaceName(string name)
    {
        if (name.Length is < 1 or > 16)
        {
            return false;
        }

        for (int index = 0; index < name.Length; index++)
        {
            char character = name[index];
            if (index == name.Length - 1 && character == '+')
            {
                return name.Length > 1;
            }

            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.' or ':' or '@'))
            {
                return false;
            }
        }

        return true;
    }
}
