using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.Cli;
using Ufw.Mock.State;

namespace Ufw.Mock.Rules;

internal sealed partial class UfwRuleParser
{
    private static readonly IReadOnlyDictionary<string, (string Port, string? Protocol)> s_services =
        new Dictionary<string, (string Port, string? Protocol)>(StringComparer.OrdinalIgnoreCase)
        {
            ["ftp-data"] = ("20", "tcp"),
            ["ftp"] = ("21", "tcp"),
            ["ssh"] = ("22", "tcp"),
            ["telnet"] = ("23", "tcp"),
            ["smtp"] = ("25", "tcp"),
            ["domain"] = ("53", null),
            ["dns"] = ("53", null),
            ["http"] = ("80", "tcp"),
            ["pop3"] = ("110", "tcp"),
            ["auth"] = ("113", "tcp"),
            ["nntp"] = ("119", "tcp"),
            ["ntp"] = ("123", "udp"),
            ["imap"] = ("143", "tcp"),
            ["snmp"] = ("161", "udp"),
            ["snmptrap"] = ("162", "udp"),
            ["https"] = ("443", "tcp"),
            ["submission"] = ("587", "tcp"),
            ["imaps"] = ("993", "tcp"),
            ["pop3s"] = ("995", "tcp"),
            ["daytime"] = ("13", null),
        };

    private static readonly HashSet<string> s_protocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "tcp", "udp", "ah", "esp", "gre", "vrrp", "ipv6", "igmp",
    };

    private static readonly HashSet<string> s_ipv4OnlyProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "ipv6", "igmp",
    };

    public ParsedRuleRequest Parse(FirewallAction action, IReadOnlyList<string> arguments, bool routed, UfwMockState state)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(state);

        if (arguments.Count == 0)
        {
            throw Error("Not enough arguments.");
        }

        FirewallDirection direction = routed ? FirewallDirection.Forward : FirewallDirection.In;
        string source = RuleSpecificationNormalizer.ANY;
        string destination = RuleSpecificationNormalizer.ANY;
        string? sourcePorts = null;
        string? destinationPorts = null;
        string? sourceInterface = null;
        string? destinationInterface = null;
        string? protocol = null;
        string? sourceApplicationName = null;
        string? destinationApplicationName = null;
        string? comment = null;
        UfwRuleLogging logging = UfwRuleLogging.None;
        bool sourceSeen = false;
        bool destinationSeen = false;
        bool protocolSeen = false;
        bool simpleTargetSeen = false;
        bool inSeen = false;
        bool outSeen = false;
        bool commentSeen = false;
        bool ruleBodySeen = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string token = arguments[index];
            switch (token.ToUpperInvariant())
            {
                case "IN":
                    if (ruleBodySeen || logging != UfwRuleLogging.None || inSeen || (!routed && outSeen))
                    {
                        throw Error("Improper rule syntax.");
                    }
                    inSeen = true;
                    if (routed)
                    {
                        if (!TryConsumeInterface(arguments, ref index, out sourceInterface))
                        {
                            throw Error("Invalid interface clause for route rule.");
                        }
                    }
                    else
                    {
                        direction = FirewallDirection.In;
                        if (TryConsumeInterface(arguments, ref index, out string? inInterface))
                        {
                            destinationInterface = inInterface;
                        }
                    }
                    break;
                case "OUT":
                    if (ruleBodySeen || logging != UfwRuleLogging.None || outSeen || (!routed && inSeen))
                    {
                        throw Error("Improper rule syntax.");
                    }
                    outSeen = true;
                    if (routed)
                    {
                        if (!TryConsumeInterface(arguments, ref index, out destinationInterface))
                        {
                            throw Error("Invalid interface clause for route rule.");
                        }
                    }
                    else
                    {
                        direction = FirewallDirection.Out;
                        if (TryConsumeInterface(arguments, ref index, out string? outInterface))
                        {
                            sourceInterface = outInterface;
                        }
                    }
                    break;
                case "ON":
                    throw Error("'on' must follow an 'in' or 'out' direction.");
                case "LOG":
                    if (ruleBodySeen)
                    {
                        throw Error("Option 'log' not allowed here.");
                    }
                    logging = SetLogging(logging, UfwRuleLogging.Log);
                    break;
                case "LOG-ALL":
                    if (ruleBodySeen)
                    {
                        throw Error("Option 'log-all' not allowed here.");
                    }
                    logging = SetLogging(logging, UfwRuleLogging.LogAll);
                    break;
                case "PROTO":
                    if (simpleTargetSeen || protocolSeen)
                    {
                        throw Error("Improper rule syntax.");
                    }
                    protocolSeen = true;
                    ruleBodySeen = true;
                    protocol = MergeProtocol(protocol, CanonicalizeProtocol(Consume(arguments, ref index, "protocol")));
                    break;
                case "FROM":
                    if (sourceSeen || simpleTargetSeen)
                    {
                        throw Error("Improper rule syntax.");
                    }
                    sourceSeen = true;
                    ruleBodySeen = true;
                    source = Consume(arguments, ref index, "source address");
                    ConsumeEndpointQualifier(arguments, ref index, state, ref sourcePorts, ref sourceApplicationName, ref protocol);
                    break;
                case "TO":
                    if (destinationSeen || simpleTargetSeen)
                    {
                        throw Error("Improper rule syntax.");
                    }
                    destinationSeen = true;
                    ruleBodySeen = true;
                    destination = Consume(arguments, ref index, "destination address");
                    ConsumeEndpointQualifier(arguments, ref index, state, ref destinationPorts, ref destinationApplicationName, ref protocol);
                    break;
                case "PORT":
                case "APP":
                    throw Error($"Unexpected '{token}' clause.");
                case "COMMENT":
                    if (commentSeen)
                    {
                        throw Error("Improper rule syntax.");
                    }
                    commentSeen = true;
                    comment = Consume(arguments, ref index, "comment text");
                    break;
                default:
                    if (sourceSeen || destinationSeen || simpleTargetSeen || protocolSeen)
                    {
                        throw Error($"Unexpected argument '{token}'.");
                    }
                    ParseSimpleTarget(token, state, ref destinationPorts, ref protocol, ref destinationApplicationName);
                    simpleTargetSeen = true;
                    ruleBodySeen = true;
                    break;
            }
        }

        ValidateAddress(source, "source");
        ValidateAddress(destination, "destination");
        ValidateInterface(sourceInterface);
        ValidateInterface(destinationInterface);
        ValidateComment(comment);
        ValidatePorts(sourcePorts, protocol);
        ValidatePorts(destinationPorts, protocol);

        if ((sourceApplicationName is not null || destinationApplicationName is not null) && protocol is not null)
        {
            throw Error("Application rules cannot specify a protocol.");
        }

        FirewallAddressFamily family = ResolveFamily(source, destination);
        if (family == FirewallAddressFamily.IPv6 && protocol is not null && s_ipv4OnlyProtocols.Contains(protocol))
        {
            throw Error($"Invalid IPv6 address with protocol '{protocol}'.");
        }

        FirewallProtocol sharedProtocol = protocol switch
        {
            "tcp" => FirewallProtocol.Tcp,
            "udp" => FirewallProtocol.Udp,
            _ => FirewallProtocol.Any,
        };
        string? extendedProtocol = sharedProtocol == FirewallProtocol.Any ? protocol : null;

        FirewallRuleSpecification specification = RuleSpecificationNormalizer.Normalize(new FirewallRuleSpecification
        {
            Action = action,
            AddressFamily = family,
            Direction = direction,
            Protocol = sharedProtocol,
            Source = source,
            SourcePorts = sourcePorts,
            SourceInterface = sourceInterface,
            Destination = destination,
            DestinationPorts = destinationPorts,
            DestinationInterface = destinationInterface,
            Comment = comment,
        });

        return new ParsedRuleRequest
        {
            Rule = new UfwMockRule
            {
                Specification = specification,
                ExtendedProtocol = extendedProtocol,
                Logging = logging,
                SourceApplicationName = sourceApplicationName,
                DestinationApplicationName = destinationApplicationName,
            },
        };
    }

    private static void ConsumeEndpointQualifier(
        IReadOnlyList<string> arguments,
        ref int index,
        UfwMockState state,
        ref string? ports,
        ref string? applicationName,
        ref string? protocol)
    {
        if (index + 1 >= arguments.Count)
        {
            return;
        }

        string qualifier = arguments[index + 1];
        if (qualifier.Equals("port", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            string port = Consume(arguments, ref index, "port");
            if (s_services.TryGetValue(port, out (string Port, string? Protocol) service))
            {
                ports = service.Port;
                protocol = MergeProtocol(protocol, service.Protocol);
            }
            else
            {
                ports = port;
            }
        }
        else if (qualifier.Equals("app", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            string name = Consume(arguments, ref index, "application name");
            UfwApplicationProfile profile = RequireApplicationProfile(state, name);
            applicationName = profile.Name;
        }
    }

    private static void ParseSimpleTarget(
        string token,
        UfwMockState state,
        ref string? destinationPorts,
        ref string? protocol,
        ref string? destinationApplicationName)
    {
        int slash = token.LastIndexOf('/');
        string target = slash >= 0 ? token[..slash] : token;
        string? targetProtocol = slash >= 0 ? CanonicalizeProtocol(token[(slash + 1)..]) : null;

        if (LooksLikePortList(target))
        {
            destinationPorts = target;
            protocol = MergeProtocol(protocol, targetProtocol);
            return;
        }

        if (s_services.TryGetValue(target, out (string Port, string? Protocol) service))
        {
            destinationPorts = service.Port;
            protocol = MergeProtocol(protocol, targetProtocol ?? service.Protocol);
            return;
        }

        if (slash < 0)
        {
            UfwApplicationProfile? applicationProfile = FindApplicationProfile(state, token);
            if (applicationProfile is not null)
            {
                destinationApplicationName = applicationProfile.Name;
                return;
            }
        }

        throw Error($"Could not find a profile matching '{token}'.");
    }

    private static string? MergeProtocol(string? current, string? requested)
    {
        if (requested is null)
        {
            return current;
        }
        if (current is null || string.Equals(current, requested, StringComparison.Ordinal))
        {
            return requested;
        }
        throw Error("Protocol mismatch (from/to or explicitly specified protocol).");
    }

    private static bool TryConsumeInterface(IReadOnlyList<string> arguments, ref int index, out string? networkInterface)
    {
        if (index + 1 >= arguments.Count || !arguments[index + 1].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            networkInterface = null;
            return false;
        }

        index++;
        networkInterface = Consume(arguments, ref index, "interface");
        return true;
    }

    private static UfwRuleLogging SetLogging(UfwRuleLogging current, UfwRuleLogging requested)
    {
        if (current != UfwRuleLogging.None)
        {
            throw Error("Only one per-rule logging mode may be specified.");
        }
        return requested;
    }

    private static string Consume(IReadOnlyList<string> arguments, ref int index, string description)
    {
        if (++index >= arguments.Count)
        {
            throw Error($"Missing {description}.");
        }
        return arguments[index];
    }

    private static string CanonicalizeProtocol(string protocol)
    {
        string canonical = protocol.ToUpperInvariant() switch
        {
            "TCP" => "tcp",
            "UDP" => "udp",
            "AH" => "ah",
            "ESP" => "esp",
            "GRE" => "gre",
            "VRRP" => "vrrp",
            "IPV6" => "ipv6",
            "IGMP" => "igmp",
            _ => throw Error($"Unsupported protocol '{protocol}'."),
        };
        return canonical;
    }

    private static void ValidateAddress(string address, string role)
    {
        if (address.Equals(RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        int slash = address.IndexOf('/');
        string host = slash < 0 ? address : address[..slash];
        if (!IPAddress.TryParse(host, out IPAddress? parsed))
        {
            throw Error($"Invalid {role} address '{address}'.");
        }

        if (slash < 0)
        {
            return;
        }

        int maxPrefix = parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (!int.TryParse(address[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix)
            || prefix < 0
            || prefix > maxPrefix)
        {
            throw Error($"Invalid {role} network '{address}'.");
        }
    }

    private static void ValidatePorts(string? ports, string? protocol)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return;
        }

        int portCount = 0;
        string[] segments = ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            throw Error("Invalid port specification.");
        }

        foreach (string segment in segments)
        {
            int colon = segment.IndexOf(':');
            if (colon < 0)
            {
                ValidatePort(segment);
                portCount++;
                continue;
            }

            ValidatePort(segment[..colon]);
            ValidatePort(segment[(colon + 1)..]);
            int start = int.Parse(segment[..colon], CultureInfo.InvariantCulture);
            int end = int.Parse(segment[(colon + 1)..], CultureInfo.InvariantCulture);
            if (start >= end)
            {
                throw Error($"Invalid port range '{segment}'.");
            }
            portCount += 2;
        }

        if (portCount > 15)
        {
            throw Error("A rule may specify at most 15 ports (ranges count as two ports).");
        }

        if (portCount > 1 && protocol is not "tcp" and not "udp")
        {
            throw Error("Multiple ports require protocol 'tcp' or 'udp'.");
        }

        if (protocol is not null && protocol is not "tcp" and not "udp")
        {
            throw Error($"Protocol '{protocol}' cannot be combined with a port clause.");
        }
    }

    private static void ValidatePort(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port)
            || port is < 1 or > 65535)
        {
            throw Error($"Invalid port '{value}'.");
        }
    }

    private static void ValidateInterface(string? networkInterface)
    {
        if (networkInterface is null)
        {
            return;
        }

        if (networkInterface is "." or ".." || !InterfaceRegex().IsMatch(networkInterface))
        {
            throw Error($"Invalid interface '{networkInterface}'.");
        }
    }

    private static void ValidateComment(string? comment)
    {
        if (comment is null)
        {
            return;
        }
        if (comment.Length > 200 || comment.Any(char.IsControl) || comment.Contains('\''))
        {
            throw Error("Invalid rule comment.");
        }
    }

    private static FirewallAddressFamily ResolveFamily(string source, string destination)
    {
        FirewallAddressFamily sourceFamily = GetCliAddressFamily(source);
        FirewallAddressFamily destinationFamily = GetCliAddressFamily(destination);
        if (sourceFamily != FirewallAddressFamily.Any
            && destinationFamily != FirewallAddressFamily.Any
            && sourceFamily != destinationFamily)
        {
            throw Error("Source and destination use different address families.");
        }
        return sourceFamily != FirewallAddressFamily.Any ? sourceFamily : destinationFamily;
    }

    private static FirewallAddressFamily GetCliAddressFamily(string address)
    {
        if (address.Equals(RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            return FirewallAddressFamily.Any;
        }

        int slash = address.IndexOf('/');
        ReadOnlySpan<char> host = slash < 0 ? address.AsSpan() : address.AsSpan(0, slash);
        if (!IPAddress.TryParse(host, out IPAddress? parsed))
        {
            throw Error($"Invalid address '{address}'.");
        }

        return parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? FirewallAddressFamily.IPv4
            : FirewallAddressFamily.IPv6;
    }

    private static bool LooksLikePortList(string value) =>
        value.Length > 0 && value.All(static character => char.IsDigit(character) || character is ',' or ':');

    private static UfwApplicationProfile RequireApplicationProfile(UfwMockState state, string name) =>
        FindApplicationProfile(state, name) ?? throw Error($"Could not find a profile matching '{name}'.");

    private static UfwApplicationProfile? FindApplicationProfile(UfwMockState state, string name) =>
        state.ApplicationProfiles.FirstOrDefault(profile => profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static UfwCliException Error(string message) => new(message);

    [GeneratedRegex("^[A-Za-z0-9_.+,=%@-]{1,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceRegex();
}
