using System.Text;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Formatting;

/// <summary>One UFW CLI rule and the physical filter declarations it expands into.</summary>
internal static class MockRuleExpansion
{
    public static string FormatTuple(UfwMockRule rule)
    {
        FirewallRuleSpecification specification = rule.Specification;
        string action = RuleSpecificationNormalizer.FormatAction(specification.Action);
        if (rule.Logging != UfwRuleLogging.None)
        {
            action += rule.Logging == UfwRuleLogging.Log ? "_log" : "_log-all";
        }
        if (specification.Direction == FirewallDirection.Forward)
        {
            action = "route:" + action;
        }

        string protocol = rule.ExtendedProtocol ?? RuleSpecificationNormalizer.FormatProtocol(specification.Protocol);
        string direction = specification.Direction switch
        {
            FirewallDirection.In => "in" + InterfaceSuffix(specification.DestinationInterface),
            FirewallDirection.Out => "out" + InterfaceSuffix(specification.SourceInterface),
            FirewallDirection.Forward => string.Join('!', new[]
            {
                specification.SourceInterface is null ? null : "in_" + specification.SourceInterface,
                specification.DestinationInterface is null ? null : "out_" + specification.DestinationInterface,
            }.Where(static value => value is not null)),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
        if (direction.Length == 0)
        {
            direction = "in";
        }

        List<string> fields =
        [
            action,
            protocol,
            specification.DestinationPorts ?? "any",
            FormatAddress(specification.Destination, specification.AddressFamily),
            specification.SourcePorts ?? "any",
            FormatAddress(specification.Source, specification.AddressFamily),
        ];
        if (rule.SourceApplicationName is not null || rule.DestinationApplicationName is not null)
        {
            fields.Add(QuoteApplication(rule.DestinationApplicationName));
            fields.Add(QuoteApplication(rule.SourceApplicationName));
        }
        fields.Add(direction);
        if (specification.Comment is not null)
        {
            fields.Add("comment=" + Convert.ToHexString(Encoding.UTF8.GetBytes(specification.Comment)));
        }
        return "### tuple ### " + string.Join(' ', fields);
    }

    public static IReadOnlyList<string> FormatDeclarations(UfwMockRule rule)
    {
        FirewallRuleSpecification specification = rule.Specification;
        string chain = specification.Direction switch
        {
            FirewallDirection.In => MockChainNames.User(specification.AddressFamily, "input"),
            FirewallDirection.Out => MockChainNames.User(specification.AddressFamily, "output"),
            FirewallDirection.Forward => MockChainNames.User(specification.AddressFamily, "forward"),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };

        string? protocol = rule.ExtendedProtocol ?? (specification.Protocol == FirewallProtocol.Any ? null : RuleSpecificationNormalizer.FormatProtocol(specification.Protocol));
        string[] protocols = protocol is null && (specification.SourcePorts is not null || specification.DestinationPorts is not null)
            ? ["tcp", "udp"]
            : [protocol ?? "all"];
        List<string> declarations = [];
        foreach (string concreteProtocol in protocols)
        {
            string predicates = FormatPredicates(specification, concreteProtocol);
            string prefix = "-A " + chain + predicates;
            if (specification.Action == FirewallAction.Limit)
            {
                declarations.Add(prefix + " -m conntrack --ctstate NEW -m recent --set");
                declarations.Add(prefix + " -m conntrack --ctstate NEW -m recent --update --seconds 30 --hitcount 6 -j " + MockChainNames.Prefix(specification.AddressFamily) + "user-limit");
                declarations.Add(prefix + " -j " + MockChainNames.Prefix(specification.AddressFamily) + "user-limit-accept");
                continue;
            }

            if (rule.Logging != UfwRuleLogging.None)
            {
                string loggingChain = MockChainNames.Prefix(specification.AddressFamily) + (specification.Direction switch
                {
                    FirewallDirection.Out => "user-logging-output",
                    FirewallDirection.Forward => "user-logging-forward",
                    _ => "user-logging-input",
                });
                declarations.Add(prefix + " -j " + loggingChain);
            }
            string target = specification.Action switch
            {
                FirewallAction.Allow => "ACCEPT",
                FirewallAction.Deny => "DROP",
                FirewallAction.Reject => "REJECT",
                _ => throw new ArgumentOutOfRangeException(nameof(rule)),
            };
            declarations.Add(prefix + " -j " + target);
        }
        return declarations;
    }

    private static string FormatPredicates(FirewallRuleSpecification specification, string protocol)
    {
        StringBuilder builder = new();
        string? ingress = specification.Direction == FirewallDirection.Out ? null : specification.DestinationInterface;
        string? egress = specification.Direction == FirewallDirection.Out ? specification.SourceInterface : null;
        if (specification.Direction == FirewallDirection.Forward)
        {
            ingress = specification.SourceInterface;
            egress = specification.DestinationInterface;
        }
        if (ingress is not null)
        {
            builder.Append(" -i ").Append(ingress);
        }
        if (egress is not null)
        {
            builder.Append(" -o ").Append(egress);
        }
        if (protocol != "all")
        {
            builder.Append(" -p ").Append(protocol);
        }
        if (specification.Source is not null && RuleSpecificationNormalizer.NormalizeAddress(specification.Source) != RuleSpecificationNormalizer.ANY)
        {
            builder.Append(" -s ").Append(specification.Source);
        }
        if (specification.Destination is not null && RuleSpecificationNormalizer.NormalizeAddress(specification.Destination) != RuleSpecificationNormalizer.ANY)
        {
            builder.Append(" -d ").Append(specification.Destination);
        }
        if (specification.SourcePorts is not null || specification.DestinationPorts is not null)
        {
            bool multiport = (specification.SourcePorts?.Contains(',') ?? false) || (specification.DestinationPorts?.Contains(',') ?? false);
            if (multiport)
            {
                builder.Append(" -m multiport");
            }
            AppendPorts(builder, specification.SourcePorts, multiport ? "--sports" : "--sport");
            AppendPorts(builder, specification.DestinationPorts, multiport ? "--dports" : "--dport");
        }
        return builder.ToString();
    }

    private static void AppendPorts(StringBuilder builder, string? ports, string option)
    {
        if (ports is not null)
        {
            builder.Append(' ').Append(option).Append(' ').Append(ports);
        }
    }

    private static string FormatAddress(string? address, FirewallAddressFamily family) =>
        RuleSpecificationNormalizer.NormalizeAddress(address) == RuleSpecificationNormalizer.ANY
            ? family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0"
            : address!;

    private static string InterfaceSuffix(string? value) => value is null ? string.Empty : "_" + value;

    private static string QuoteApplication(string? value) => value is null ? "-" : '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
}
