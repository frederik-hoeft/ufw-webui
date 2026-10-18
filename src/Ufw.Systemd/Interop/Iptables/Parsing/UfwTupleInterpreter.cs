using System.Globalization;
using System.Text;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Interop.Iptables.Grammars;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>
/// Reads one <c>### tuple ###</c> body into the mutable UFW rule model when the tuple fits it.
/// The tuple is still accepted when the action is representable but a field, such as an
/// unsupported protocol, cannot be a <see cref="FirewallRuleSpecification"/>.
/// </summary>
internal static class UfwTupleInterpreter
{
    public static bool TryInterpret(
        string body,
        FirewallAddressFamily family,
        out FirewallRuleSpecification? specification,
        out string? ruleId)
    {
        ArgumentNullException.ThrowIfNull(body);
        specification = null;
        ruleId = null;
        if (family is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            return false;
        }

        if (!UfwTupleBodyGrammar.Instance.TryParse(body, out IReadOnlyList<string>? parsedFields))
        {
            return false;
        }

        string[] fields = parsedFields.ToArray();
        string? encodedComment = null;
        if (fields.Length > 0 && fields[^1].StartsWith("comment=", StringComparison.Ordinal))
        {
            encodedComment = fields[^1]["comment=".Length..];
            fields = fields[..^1];
        }

        if (fields.Length is < 6 or > 9)
        {
            return false;
        }

        if (!TryAction(fields[0], out FirewallAction action, out bool forward))
        {
            return false;
        }

        string directionField = "in";
        string interfaceIn = string.Empty;
        string interfaceOut = string.Empty;
        if (fields.Length is 7 or 9 && !TryInterfaces(fields[^1], out directionField, out interfaceIn, out interfaceOut))
        {
            return false;
        }

        if (directionField is not ("in" or "out"))
        {
            return false;
        }

        string? comment = null;
        if (!string.IsNullOrEmpty(encodedComment) && !TryDecodeComment(encodedComment, out comment))
        {
            return false;
        }

        if (!TryRepresent(
                family,
                action,
                forward,
                directionField,
                fields[1],
                fields[2],
                fields[3],
                fields[4],
                fields[5],
                interfaceIn,
                interfaceOut,
                comment,
                out specification))
        {
            specification = null;
            ruleId = null;
            return true;
        }

        ruleId = RuleIdentity.Compute(specification);
        return true;
    }

    private static bool TryAction(string field, out FirewallAction action, out bool forward)
    {
        forward = false;
        string actionField = field;
        int colon = actionField.IndexOf(':');
        if (colon >= 0)
        {
            if (colon == 0 || actionField[(colon + 1)..].Contains(':') || actionField[..colon] != "route")
            {
                action = default;
                return false;
            }

            forward = true;
            actionField = actionField[(colon + 1)..];
        }

        int underscore = actionField.IndexOf('_');
        if (underscore >= 0)
        {
            string logType = actionField[(underscore + 1)..];
            if (logType is not ("log" or "log-all"))
            {
                action = default;
                return false;
            }

            actionField = actionField[..underscore];
        }

        switch (actionField)
        {
            case "allow":
                action = FirewallAction.Allow;
                return true;
            case "deny":
                action = FirewallAction.Deny;
                return true;
            case "reject":
                action = FirewallAction.Reject;
                return true;
            case "limit":
                action = FirewallAction.Limit;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private static bool TryInterfaces(string field, out string direction, out string interfaceIn, out string interfaceOut)
    {
        direction = "in";
        interfaceIn = string.Empty;
        interfaceOut = string.Empty;
        int bang = field.IndexOf('!');
        if (bang >= 0)
        {
            if (!TryPrefixedInterface(field[..bang], "in", out interfaceIn)
                || !TryPrefixedInterface(field[(bang + 1)..], "out", out interfaceOut))
            {
                return false;
            }

            direction = "in";
            return true;
        }

        int underscore = field.IndexOf('_');
        if (underscore < 0)
        {
            direction = field;
            return direction is "in" or "out";
        }

        direction = field[..underscore];
        string interfaceName = field[(underscore + 1)..];
        if (interfaceName.Length == 0 || direction is not ("in" or "out"))
        {
            return false;
        }

        if (direction == "in")
        {
            interfaceIn = interfaceName;
        }
        else
        {
            interfaceOut = interfaceName;
        }

        return true;
    }

    private static bool TryPrefixedInterface(string field, string expectedDirection, out string interfaceName)
    {
        interfaceName = string.Empty;
        string prefix = expectedDirection + "_";
        if (!field.StartsWith(prefix, StringComparison.Ordinal) || field.Length == prefix.Length)
        {
            return false;
        }

        interfaceName = field[prefix.Length..];
        return true;
    }

    private static bool TryDecodeComment(string hex, out string? comment)
    {
        comment = null;
        if (hex.Length % 2 != 0)
        {
            return false;
        }

        try
        {
            byte[] bytes = Convert.FromHexString(hex);
            comment = RuleSpecificationNormalizer.NormalizeComment(Encoding.UTF8.GetString(bytes));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryRepresent(
        FirewallAddressFamily family,
        FirewallAction action,
        bool forward,
        string directionField,
        string protocolField,
        string destinationPorts,
        string destination,
        string sourcePorts,
        string source,
        string interfaceIn,
        string interfaceOut,
        string? comment,
        out FirewallRuleSpecification specification)
    {
        specification = new FirewallRuleSpecification
        {
            Action = action,
            AddressFamily = family,
            Direction = forward
                ? FirewallDirection.Forward
                : directionField == "out" ? FirewallDirection.Out : FirewallDirection.In,
        };

        if (!TryProtocol(protocolField, out FirewallProtocol protocol))
        {
            return false;
        }

        if (!TryPorts(destinationPorts, out string? normalizedDestinationPorts)
            || !TryPorts(sourcePorts, out string? normalizedSourcePorts))
        {
            return false;
        }

        string normalizedSource = RuleSpecificationNormalizer.NormalizeAddress(source);
        string normalizedDestination = RuleSpecificationNormalizer.NormalizeAddress(destination);
        if (!AddressFits(normalizedSource, family) || !AddressFits(normalizedDestination, family))
        {
            return false;
        }

        string? sourceInterface;
        string? destinationInterface;
        if (forward)
        {
            sourceInterface = NullIfEmpty(interfaceIn);
            destinationInterface = NullIfEmpty(interfaceOut);
        }
        else if (specification.Direction == FirewallDirection.In)
        {
            if (interfaceOut.Length > 0)
            {
                return false;
            }

            sourceInterface = null;
            destinationInterface = NullIfEmpty(interfaceIn);
        }
        else
        {
            if (interfaceIn.Length > 0)
            {
                return false;
            }

            sourceInterface = NullIfEmpty(interfaceOut);
            destinationInterface = null;
        }

        specification.Protocol = protocol;
        specification.Source = normalizedSource;
        specification.Destination = normalizedDestination;
        specification.SourcePorts = normalizedSourcePorts;
        specification.DestinationPorts = normalizedDestinationPorts;
        specification.SourceInterface = sourceInterface;
        specification.DestinationInterface = destinationInterface;
        specification.Comment = comment;
        specification = RuleSpecificationNormalizer.Normalize(specification);
        return true;
    }

    private static bool TryProtocol(string field, out FirewallProtocol protocol)
    {
        switch (field)
        {
            case "any":
                protocol = FirewallProtocol.Any;
                return true;
            case "tcp":
                protocol = FirewallProtocol.Tcp;
                return true;
            case "udp":
                protocol = FirewallProtocol.Udp;
                return true;
            default:
                protocol = default;
                return false;
        }
    }

    private static bool TryPorts(string field, out string? ports)
    {
        if (field == "any")
        {
            ports = null;
            return true;
        }

        string? normalized = RuleSpecificationNormalizer.NormalizePorts(field);
        if (string.IsNullOrEmpty(normalized))
        {
            ports = null;
            return false;
        }

        foreach (string part in normalized.Split(','))
        {
            int colon = part.IndexOf(':');
            if (colon < 0)
            {
                if (!IsPort(part))
                {
                    ports = null;
                    return false;
                }

                continue;
            }

            if (!IsPort(part[..colon]) || !IsPort(part[(colon + 1)..]))
            {
                ports = null;
                return false;
            }
        }

        ports = normalized;
        return true;
    }

    private static bool IsPort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port is >= 1 and <= 65535;

    private static bool AddressFits(string address, FirewallAddressFamily family)
    {
        if (address == RuleSpecificationNormalizer.ANY)
        {
            return true;
        }

        if (address == RuleSpecificationNormalizer.ANY)
        {
            return true;
        }

        return RuleSpecificationNormalizer.GetAddressFamily(address) == family;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
