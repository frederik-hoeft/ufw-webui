using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ufw.Ipc.Shared.Model.Domain.Rules;

public static class RuleSpecificationNormalizer
{
    public const string ANY = "any";

    public static FirewallRuleSpecification Normalize(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        string source = NormalizeAddress(specification.Source);
        string destination = NormalizeAddress(specification.Destination);
        return new FirewallRuleSpecification
        {
            Action = specification.Action,
            AddressFamily = ResolveAddressFamily(specification.AddressFamily, source, destination),
            Direction = specification.Direction,
            Protocol = specification.Protocol,
            Source = source,
            SourcePorts = NormalizePorts(specification.SourcePorts),
            SourceInterface = NormalizeInterface(specification.SourceInterface),
            Destination = destination,
            DestinationPorts = NormalizePorts(specification.DestinationPorts),
            DestinationInterface = NormalizeInterface(specification.DestinationInterface),
            Comment = NormalizeComment(specification.Comment),
        };
    }

    public static string CanonicalizeIdentity(FirewallRuleSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        FirewallRuleSpecification normalized = Normalize(specification);
        StringBuilder builder = new();
        builder.Append("rule-identity/2\n");
        AppendField(builder, "action", FormatAction(normalized.Action));
        AppendField(builder, "addressFamily", FormatAddressFamily(normalized.AddressFamily));
        AppendField(builder, "destination", normalized.Destination ?? ANY);
        AppendField(builder, "destinationInterface", normalized.DestinationInterface ?? string.Empty);
        AppendField(builder, "destinationPorts", normalized.DestinationPorts ?? string.Empty);
        AppendField(builder, "direction", FormatDirection(normalized.Direction));
        AppendField(builder, "protocol", FormatProtocol(normalized.Protocol));
        AppendField(builder, "source", normalized.Source ?? ANY);
        AppendField(builder, "sourceInterface", normalized.SourceInterface ?? string.Empty);
        AppendField(builder, "sourcePorts", normalized.SourcePorts ?? string.Empty);
        return builder.ToString();
    }

    public static string FormatAction(FirewallAction action) => action switch
    {
        FirewallAction.Allow => "allow",
        FirewallAction.Deny => "deny",
        FirewallAction.Reject => "reject",
        FirewallAction.Limit => "limit",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported firewall action.")
    };

    public static string FormatAddressFamily(FirewallAddressFamily addressFamily) => addressFamily switch
    {
        FirewallAddressFamily.Any => "any",
        FirewallAddressFamily.IPv4 => "ipv4",
        FirewallAddressFamily.IPv6 => "ipv6",
        _ => throw new ArgumentOutOfRangeException(nameof(addressFamily), addressFamily, "Unsupported firewall address family.")
    };

    public static string FormatDirection(FirewallDirection direction) => direction switch
    {
        FirewallDirection.In => "in",
        FirewallDirection.Out => "out",
        FirewallDirection.Forward => "forward",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported firewall direction.")
    };

    public static string FormatProtocol(FirewallProtocol protocol) => protocol switch
    {
        FirewallProtocol.Any => "any",
        FirewallProtocol.Tcp => "tcp",
        FirewallProtocol.Udp => "udp",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported firewall protocol.")
    };

    public static string NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return ANY;
        }

        string trimmed = address.Trim();
        if (trimmed.Equals(ANY, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("Anywhere", StringComparison.OrdinalIgnoreCase))
        {
            return ANY;
        }

        if (trimmed.Equals("0.0.0.0", StringComparison.Ordinal))
        {
            return ANY;
        }

        int slash = trimmed.IndexOf('/');
        string host = slash < 0 ? trimmed : trimmed[..slash];
        if (!IPAddress.TryParse(host, out IPAddress? parsed))
        {
            return trimmed;
        }

        if (parsed.AddressFamily == AddressFamily.InterNetworkV6 && parsed.ScopeId != 0)
        {
            return trimmed;
        }

        int maxPrefix = parsed.AddressFamily switch
        {
            AddressFamily.InterNetwork => 32,
            AddressFamily.InterNetworkV6 => 128,
            _ => -1,
        };
        if (maxPrefix < 0)
        {
            return trimmed;
        }

        int prefix = maxPrefix;
        if (slash >= 0
            && (!int.TryParse(trimmed[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out prefix)
                || prefix < 0
                || prefix > maxPrefix))
        {
            return trimmed;
        }

        if (prefix == 0)
        {
            return ANY;
        }

        IPAddress network = ApplyPrefix(parsed, prefix);
        string canonicalHost = network.ToString();
        return prefix == maxPrefix ? canonicalHost : $"{canonicalHost}/{prefix.ToString(CultureInfo.InvariantCulture)}";
    }

    public static string? NormalizePorts(string? ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return null;
        }

        List<(int Start, int End)> ranges = [];
        foreach (string part in ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = part.IndexOf(':');
            if (colon < 0)
            {
                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int port))
                {
                    return ports.Trim();
                }
                if (port is < 1 or > 65535)
                {
                    return ports.Trim();
                }
                ranges.Add((port, port));
                continue;
            }

            if (!int.TryParse(part.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out int start)
                || !int.TryParse(part.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int end))
            {
                return ports.Trim();
            }
            if (start is < 1 or > 65535 || end is < 1 or > 65535 || start > end)
            {
                return ports.Trim();
            }
            ranges.Add((start, end));
        }

        if (ranges.Count == 0)
        {
            return null;
        }

        ranges.Sort(static (left, right) => left.Start != right.Start
            ? left.Start.CompareTo(right.Start)
            : left.End.CompareTo(right.End));

        List<(int Start, int End)> merged = [];
        foreach ((int Start, int End) current in ranges)
        {
            if (merged.Count == 0)
            {
                merged.Add(current);
                continue;
            }

            (int start, int end) = merged[^1];
            if (current.Start <= end + 1)
            {
                merged[^1] = (start, Math.Max(end, current.End));
            }
            else
            {
                merged.Add(current);
            }
        }

        return string.Join(',', merged.Select(static range => range.Start == range.End
            ? range.Start.ToString(CultureInfo.InvariantCulture)
            : $"{range.Start.ToString(CultureInfo.InvariantCulture)}:{range.End.ToString(CultureInfo.InvariantCulture)}"));
    }

    public static string? NormalizeInterface(string? networkInterface)
    {
        if (string.IsNullOrWhiteSpace(networkInterface))
        {
            return null;
        }

        return networkInterface.Trim();
    }

    public static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        return comment.Trim();
    }

    public static FirewallAddressFamily GetAddressFamily(string? address)
    {
        string normalized = NormalizeAddress(address);
        if (normalized == ANY)
        {
            return FirewallAddressFamily.Any;
        }

        int slash = normalized.IndexOf('/');
        string host = slash < 0 ? normalized : normalized[..slash];
        if (!IPAddress.TryParse(host, out IPAddress? parsed))
        {
            return FirewallAddressFamily.Any;
        }

        return parsed.AddressFamily switch
        {
            AddressFamily.InterNetwork => FirewallAddressFamily.IPv4,
            AddressFamily.InterNetworkV6 => FirewallAddressFamily.IPv6,
            _ => FirewallAddressFamily.Any,
        };
    }

    private static FirewallAddressFamily ResolveAddressFamily(FirewallAddressFamily declared, string source, string destination)
    {
        if (declared != FirewallAddressFamily.Any)
        {
            return declared;
        }

        FirewallAddressFamily sourceFamily = GetAddressFamily(source);
        FirewallAddressFamily destinationFamily = GetAddressFamily(destination);
        if (sourceFamily == FirewallAddressFamily.Any)
        {
            return destinationFamily;
        }
        if (destinationFamily == FirewallAddressFamily.Any || sourceFamily == destinationFamily)
        {
            return sourceFamily;
        }

        return FirewallAddressFamily.Any;
    }

    private static IPAddress ApplyPrefix(IPAddress address, int prefix)
    {
        byte[] bytes = address.GetAddressBytes();
        int fullBytes = prefix / 8;
        int remainingBits = prefix % 8;
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

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }
}
