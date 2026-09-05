using System.Globalization;
using System.Text;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.State;

namespace Ufw.Mock.Formatting;

internal static class UfwOutputFormatter
{
    public static string FormatStatus(UfwMockState state, bool numbered, bool verbose)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Enabled)
        {
            return "Status: inactive";
        }

        StringBuilder builder = new();
        builder.AppendLine("Status: active");
        if (verbose)
        {
            string logging = state.LoggingLevel.Equals("off", StringComparison.OrdinalIgnoreCase)
                ? "Logging: off"
                : $"Logging: on ({state.LoggingLevel})";
            builder.AppendLine(logging);
            builder.Append("Default: ");
            builder.Append(state.DefaultIncomingPolicy);
            builder.Append(" (incoming), ");
            builder.Append(state.DefaultOutgoingPolicy);
            builder.Append(" (outgoing), ");
            builder.Append(state.DefaultRoutedPolicy);
            builder.AppendLine(" (routed)");
            builder.Append("New profiles: ");
            builder.AppendLine(state.DefaultApplicationPolicy);
        }

        builder.AppendLine();
        string prefix = numbered ? "     " : string.Empty;
        builder.Append(prefix);
        builder.AppendLine("To                         Action      From");
        builder.Append(prefix);
        builder.AppendLine("--                         ------      ----");

        for (int index = 0; index < state.Rules.Count; index++)
        {
            UfwMockRule rule = state.Rules[index];
            string rowPrefix = numbered ? $"[{(index + 1).ToString(CultureInfo.InvariantCulture),2}] " : string.Empty;
            builder.Append(rowPrefix);
            builder.AppendLine(FormatRuleRow(rule));
        }

        return builder.ToString().TrimEnd();
    }

    public static string FormatRuleRow(UfwMockRule rule)
    {
        FirewallRuleSpecification specification = rule.Specification;
        string destination = FormatEndpoint(
            specification.Destination,
            specification.DestinationPorts,
            specification.DestinationInterface,
            specification.AddressFamily,
            rule,
            destination: true);
        string source = FormatEndpoint(
            specification.Source,
            specification.SourcePorts,
            specification.SourceInterface,
            specification.AddressFamily,
            rule,
            destination: false);
        string action = $"{RuleSpecificationNormalizer.FormatAction(specification.Action).ToUpperInvariant()} {FormatDirection(specification.Direction)}";

        string row = string.Format(CultureInfo.InvariantCulture, "{0,-27} {1,-11} {2}", destination, action, source);
        if (!string.IsNullOrEmpty(specification.Comment))
        {
            row += " # " + specification.Comment;
        }
        return row.TrimEnd();
    }

    public static string FormatAdded(UfwMockState state)
    {
        StringBuilder builder = new();
        builder.AppendLine("Added user rules (see 'ufw status' for running firewall):");
        foreach (UfwMockRule rule in state.Rules)
        {
            builder.Append("ufw ");
            builder.AppendLine(FormatRuleCommand(rule));
        }
        return builder.ToString().TrimEnd();
    }

    public static string FormatUserRules(UfwMockState state)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Ufw.Mock synthetic user-rules report");
        for (int index = 0; index < state.Rules.Count; index++)
        {
            builder.Append("# ");
            builder.Append((index + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.AppendLine(FormatRuleCommand(state.Rules[index]));
        }
        return builder.ToString().TrimEnd();
    }

    public static string FormatRuleCommand(UfwMockRule rule)
    {
        FirewallRuleSpecification specification = rule.Specification;
        List<string> arguments = [];
        if (specification.Direction == FirewallDirection.Forward)
        {
            arguments.Add("route");
        }
        arguments.Add(RuleSpecificationNormalizer.FormatAction(specification.Action));

        switch (specification.Direction)
        {
            case FirewallDirection.In:
                arguments.Add("in");
                if (specification.DestinationInterface is not null)
                {
                    arguments.Add("on");
                    arguments.Add(specification.DestinationInterface);
                }
                break;
            case FirewallDirection.Out:
                arguments.Add("out");
                if (specification.SourceInterface is not null)
                {
                    arguments.Add("on");
                    arguments.Add(specification.SourceInterface);
                }
                break;
            case FirewallDirection.Forward:
                if (specification.SourceInterface is not null)
                {
                    arguments.Add("in");
                    arguments.Add("on");
                    arguments.Add(specification.SourceInterface);
                }
                if (specification.DestinationInterface is not null)
                {
                    arguments.Add("out");
                    arguments.Add("on");
                    arguments.Add(specification.DestinationInterface);
                }
                break;
        }

        arguments.Add("from");
        arguments.Add(FormatAddressForCommand(specification.Source, specification.AddressFamily));
        if (rule.SourceApplicationName is not null)
        {
            arguments.Add("app");
            arguments.Add(QuoteIfNeeded(rule.SourceApplicationName));
        }
        else if (specification.SourcePorts is not null)
        {
            arguments.Add("port");
            arguments.Add(specification.SourcePorts);
        }
        arguments.Add("to");
        arguments.Add(FormatAddressForCommand(specification.Destination, specification.AddressFamily));
        if (rule.DestinationApplicationName is not null)
        {
            arguments.Add("app");
            arguments.Add(QuoteIfNeeded(rule.DestinationApplicationName));
        }
        else if (specification.DestinationPorts is not null)
        {
            arguments.Add("port");
            arguments.Add(specification.DestinationPorts);
        }

        string? protocol = rule.ExtendedProtocol ?? (specification.Protocol == FirewallProtocol.Any
            ? null
            : RuleSpecificationNormalizer.FormatProtocol(specification.Protocol));
        if (protocol is not null)
        {
            arguments.Add("proto");
            arguments.Add(protocol);
        }
        if (rule.Logging != UfwRuleLogging.None)
        {
            arguments.Add(rule.Logging == UfwRuleLogging.Log ? "log" : "log-all");
        }
        if (specification.Comment is not null)
        {
            arguments.Add("comment");
            arguments.Add(Quote(specification.Comment));
        }
        return string.Join(' ', arguments);
    }

    private static string FormatEndpoint(
        string? address,
        string? ports,
        string? networkInterface,
        FirewallAddressFamily family,
        UfwMockRule rule,
        bool destination)
    {
        string? applicationName = destination ? rule.DestinationApplicationName : rule.SourceApplicationName;
        if (applicationName is not null)
        {
            return applicationName + FormatV6Hint(family) + FormatInterface(networkInterface);
        }

        string normalizedAddress = RuleSpecificationNormalizer.NormalizeAddress(address);
        string endpoint = normalizedAddress == RuleSpecificationNormalizer.ANY
            ? ports is null ? "Anywhere" : ports
            : ports is null ? normalizedAddress : normalizedAddress + " " + ports;

        string? protocol = rule.ExtendedProtocol ?? (rule.Specification.Protocol == FirewallProtocol.Any
            ? null
            : RuleSpecificationNormalizer.FormatProtocol(rule.Specification.Protocol));
        if (ports is not null && protocol is not null && protocol is "tcp" or "udp")
        {
            endpoint += "/" + protocol;
        }
        endpoint += FormatV6Hint(family);
        endpoint += FormatInterface(networkInterface);
        return endpoint;
    }

    private static string FormatDirection(FirewallDirection direction) => direction switch
    {
        FirewallDirection.In => "IN",
        FirewallDirection.Out => "OUT",
        FirewallDirection.Forward => "FWD",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    private static string FormatV6Hint(FirewallAddressFamily family) => family == FirewallAddressFamily.IPv6 ? " (v6)" : string.Empty;

    private static string FormatInterface(string? networkInterface) => networkInterface is null ? string.Empty : " on " + networkInterface;

    private static string FormatAddressForCommand(string? address, FirewallAddressFamily family)
    {
        string normalized = RuleSpecificationNormalizer.NormalizeAddress(address);
        if (normalized != RuleSpecificationNormalizer.ANY)
        {
            return normalized;
        }
        return family switch
        {
            FirewallAddressFamily.IPv4 => "0.0.0.0/0",
            FirewallAddressFamily.IPv6 => "::/0",
            _ => "any",
        };
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string QuoteIfNeeded(string value) => value.Contains(' ') ? Quote(value) : value;
}
