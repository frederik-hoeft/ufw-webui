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
        return new FirewallRuleSpecification
        {
            Action = specification.Action,
            Direction = specification.Direction,
            Protocol = specification.Protocol,
            Source = NormalizeAddress(specification.Source),
            SourcePorts = NormalizePorts(specification.SourcePorts),
            SourceInterface = NormalizeInterface(specification.SourceInterface),
            Destination = NormalizeAddress(specification.Destination),
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
        builder.Append("rule-identity/1\n");
        AppendField(builder, "action", FormatAction(normalized.Action));
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
            || trimmed.Equals("Anywhere", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("0.0.0.0/0", StringComparison.Ordinal)
            || trimmed.Equals("0.0.0.0", StringComparison.Ordinal))
        {
            return ANY;
        }

        int slash = trimmed.IndexOf('/');
        if (slash < 0)
        {
            return TryRewriteIPv4(trimmed, out string? rewritten) ? rewritten : trimmed;
        }

        string host = trimmed[..slash];
        string prefix = trimmed[(slash + 1)..];
        if (TryRewriteIPv4(host, out string? rewrittenHost)
            && int.TryParse(prefix, NumberStyles.None, CultureInfo.InvariantCulture, out int bits)
            && bits is >= 0 and <= 32)
        {
            if (bits == 0)
            {
                return ANY;
            }

            return rewrittenHost + "/" + bits.ToString(CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    public static string? NormalizePorts(string? ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return null;
        }

        string[] parts = ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        Array.Sort(parts, StringComparer.Ordinal);
        return string.Join(',', parts);
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

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append('=');
        builder.Append(value);
        builder.Append('\n');
    }

    private static bool TryRewriteIPv4(string host, out string rewritten)
    {
        if (IPAddress.TryParse(host, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork)
        {
            rewritten = address.ToString();
            return true;
        }

        rewritten = host;
        return false;
    }
}
