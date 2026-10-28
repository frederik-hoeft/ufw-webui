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

    public static IReadOnlyList<MockIptablesDeclaration> Expand(UfwMockRule rule)
    {
        FirewallRuleSpecification specification = rule.Specification;
        string chain = specification.Direction switch
        {
            FirewallDirection.In => MockChainNames.User(specification.AddressFamily, "input"),
            FirewallDirection.Out => MockChainNames.User(specification.AddressFamily, "output"),
            FirewallDirection.Forward => MockChainNames.User(specification.AddressFamily, "forward"),
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
        string direction = specification.Direction switch
        {
            FirewallDirection.In => "input",
            FirewallDirection.Out => "output",
            FirewallDirection.Forward => "forward",
            _ => throw new ArgumentOutOfRangeException(nameof(rule)),
        };
        string chainPrefix = MockChainNames.Prefix(specification.AddressFamily);
        string? protocol = rule.ExtendedProtocol ?? (specification.Protocol == FirewallProtocol.Any ? null : RuleSpecificationNormalizer.FormatProtocol(specification.Protocol));
        string[] protocols = protocol is null && (specification.SourcePorts is not null || specification.DestinationPorts is not null)
            ? ["tcp", "udp"]
            : [protocol ?? "all"];
        string? comment = FormatApplicationComment(rule);
        List<MockIptablesDeclaration> declarations = [];
        foreach (string concreteProtocol in protocols)
        {
            string predicates = FormatPredicates(specification, concreteProtocol).TrimStart();
            string prefix = predicates.Length == 0 ? string.Empty : predicates + " ";
            if (specification.Action == FirewallAction.Limit)
            {
                // UFW uses the recent extension's update rule and a fallthrough accept.
                // IPv6 limit rules are not installed by UFW 0.36.x.
                if (specification.AddressFamily == FirewallAddressFamily.IPv6)
                {
                    continue;
                }
                string recent = prefix + "-m conntrack --ctstate NEW -m recent --update --seconds 30 --hitcount 6";
                declarations.Add(new(chain, recent + " -j " + chainPrefix + "user-limit", comment));
                declarations.Add(new(chain, prefix + "-j " + chainPrefix + "user-limit-accept", comment));
                continue;
            }

            if (rule.Logging != UfwRuleLogging.None)
            {
                string loggingChain = chainPrefix + "user-logging-" + direction;
                string label = specification.Action == FirewallAction.Allow ? "ALLOW" : "BLOCK";
                declarations.Add(new(loggingChain, prefix + "-m limit --limit 3/min --limit-burst 10 -j LOG --log-prefix \"[UFW " + label + "] \""));
                declarations.Add(new(loggingChain, prefix + "-j RETURN"));
                declarations.Add(new(chain, prefix + "-j " + loggingChain));
            }

            string target = specification.Action switch
            {
                FirewallAction.Allow => "ACCEPT",
                FirewallAction.Deny => "DROP",
                FirewallAction.Reject => "REJECT",
                _ => throw new ArgumentOutOfRangeException(nameof(rule)),
            };
            string rejectOptions = specification.Action == FirewallAction.Reject && concreteProtocol == "tcp" ? " --reject-with tcp-reset" : string.Empty;
            declarations.Add(new(chain, prefix + "-j " + target + rejectOptions, comment));
        }
        return declarations;
    }

    private static string? FormatApplicationComment(UfwMockRule rule)
    {
        List<string> components = [];
        if (rule.DestinationApplicationName is not null)
        {
            components.Add("dapp_" + rule.DestinationApplicationName.Replace(" ", "%20", StringComparison.Ordinal));
        }
        if (rule.SourceApplicationName is not null)
        {
            components.Add("sapp_" + rule.SourceApplicationName.Replace(" ", "%20", StringComparison.Ordinal));
        }
        return components.Count == 0 ? null : string.Join(',', components);
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
        bool multiport = (specification.SourcePorts?.Contains(',') ?? false)
            || (specification.DestinationPorts?.Contains(',') ?? false)
            || (specification.SourcePorts?.Contains(':') ?? false)
            || (specification.DestinationPorts?.Contains(':') ?? false);
        if (multiport)
        {
            if (specification.DestinationPorts is not null)
            {
                builder.Append(" -m multiport --dports ").Append(specification.DestinationPorts);
            }
            if (specification.SourcePorts is not null)
            {
                builder.Append(" -m multiport --sports ").Append(specification.SourcePorts);
            }
        }
        if (specification.Destination is not null && RuleSpecificationNormalizer.NormalizeAddress(specification.Destination) != RuleSpecificationNormalizer.ANY)
        {
            builder.Append(" -d ").Append(specification.Destination);
        }
        if (!multiport)
        {
            AppendPorts(builder, specification.DestinationPorts, "--dport");
        }
        if (specification.Source is not null && RuleSpecificationNormalizer.NormalizeAddress(specification.Source) != RuleSpecificationNormalizer.ANY)
        {
            builder.Append(" -s ").Append(specification.Source);
        }
        if (!multiport)
        {
            AppendPorts(builder, specification.SourcePorts, "--sport");
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

    private static string QuoteApplication(string? value) => value is null ? "-" : value.Replace(" ", "%20", StringComparison.Ordinal);
}
