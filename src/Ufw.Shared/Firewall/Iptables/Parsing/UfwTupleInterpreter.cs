using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Shared.Firewall.Iptables.Grammars;

namespace Ufw.Shared.Firewall.Iptables.Parsing;

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

        if (!UfwTupleBodyGrammar.Instance.TryParse(body, out UfwTupleSyntax? tuple))
        {
            return false;
        }

        if (!TryRepresent(family, tuple, out specification))
        {
            specification = null;
            ruleId = null;
            return true;
        }

        ruleId = RuleIdentity.Compute(specification);
        return true;
    }

    private static bool TryRepresent(FirewallAddressFamily family, UfwTupleSyntax tuple, out FirewallRuleSpecification specification)
    {
        specification = new FirewallRuleSpecification
        {
            Action = tuple.Action,
            AddressFamily = family,
            Direction = tuple.Forward
                ? FirewallDirection.Forward
                : tuple.Direction == "out" ? FirewallDirection.Out : FirewallDirection.In,
        };

        if (!TryProtocol(tuple.Protocol, out FirewallProtocol protocol))
        {
            return false;
        }

        if (!TryPorts(tuple.DestinationPorts, out string? normalizedDestinationPorts)
            || !TryPorts(tuple.SourcePorts, out string? normalizedSourcePorts))
        {
            return false;
        }

        string normalizedSource = RuleSpecificationNormalizer.NormalizeAddress(tuple.Source);
        string normalizedDestination = RuleSpecificationNormalizer.NormalizeAddress(tuple.Destination);
        if (!AddressFits(normalizedSource, family) || !AddressFits(normalizedDestination, family))
        {
            return false;
        }

        string? sourceInterface;
        string? destinationInterface;
        if (tuple.Forward)
        {
            sourceInterface = NullIfEmpty(tuple.InterfaceIn);
            destinationInterface = NullIfEmpty(tuple.InterfaceOut);
        }
        else if (specification.Direction == FirewallDirection.In)
        {
            if (tuple.InterfaceOut.Length > 0)
            {
                return false;
            }

            sourceInterface = null;
            destinationInterface = NullIfEmpty(tuple.InterfaceIn);
        }
        else
        {
            if (tuple.InterfaceIn.Length > 0)
            {
                return false;
            }

            sourceInterface = NullIfEmpty(tuple.InterfaceOut);
            destinationInterface = null;
        }

        specification.Protocol = protocol;
        specification.Source = normalizedSource;
        specification.Destination = normalizedDestination;
        specification.SourcePorts = normalizedSourcePorts;
        specification.DestinationPorts = normalizedDestinationPorts;
        specification.SourceInterface = sourceInterface;
        specification.DestinationInterface = destinationInterface;
        specification.Comment = tuple.Comment;
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

        bool recognized = IptablesPredicateNormalizer.TryCanonicalPorts(field, out string normalized);
        ports = recognized ? normalized : null;
        return recognized;
    }

    private static bool AddressFits(string address, FirewallAddressFamily family)
    {
        if (address == RuleSpecificationNormalizer.ANY)
        {
            return true;
        }

        return RuleSpecificationNormalizer.GetAddressFamily(address) == family;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
