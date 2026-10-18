using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>
/// Classifies an iptables rule's tokens into structured UFW predicates and opaque remainder.
/// One classifier is shared by every source that reuses the rule-line grammar.
/// </summary>
internal static class IptablesRuleInterpreter
{
    private static readonly HashSet<string> s_transparentModules = new(StringComparer.Ordinal)
    {
        "tcp",
        "udp",
        "udplite",
        "sctp",
        "dccp",
        "multiport",
    };

    public static bool TryInterpret(
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        out IReadOnlyList<FilterClause> clauses,
        out FilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        List<FilterClause> parsed = [];
        List<string> opaque = [];
        string? openModule = null;
        FilterTargetKind targetKind = FilterTargetKind.None;
        string? targetName = null;
        List<string>? targetArguments = null;
        int index = 0;

        while (index < tokens.Count)
        {
            if (IsClauseStarter(tokens[index]))
            {
                if (!TryConsumeClause(tokens, family, ref index, ref openModule, opaque, parsed, ref targetKind, ref targetName, ref targetArguments))
                {
                    clauses = [];
                    target = UnavailableTarget();
                    return false;
                }

                continue;
            }

            if (tokens[index].Kind == IptablesTokenKind.Negation)
            {
                AppendOpaqueToken(opaque, ref openModule, "!");
                index++;
                continue;
            }

            AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
            index++;
        }

        FlushOpaque(parsed, opaque, ref openModule);
        openModule = null;
        clauses = parsed;
        target = new FilterTarget
        {
            Kind = targetKind,
            Name = targetName,
            IsChainReference = false,
            Arguments = targetArguments is null || targetArguments.Count == 0 ? null : string.Join(' ', targetArguments),
        };
        return targetKind == FilterTargetKind.None || targetName is not null;
    }

    private static bool TryConsumeClause(
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        ref int index,
        ref string? openModule,
        List<string> opaque,
        List<FilterClause> clauses,
        ref FilterTargetKind targetKind,
        ref string? targetName,
        ref List<string>? targetArguments)
    {
        bool negated = false;
        if (tokens[index].Kind == IptablesTokenKind.Negation)
        {
            negated = true;
            index++;
            if (index >= tokens.Count || !IsOption(tokens[index]))
            {
                return false;
            }
        }

        string option = CanonicalOption(tokens[index]);
        int optionIndex = index;
        index++;

        switch (option)
        {
            case "jump":
            case "goto":
                return TryConsumeTarget(option, negated, tokens, ref index, ref openModule, opaque, clauses, ref targetKind, ref targetName, ref targetArguments);
            case "match":
                return TryConsumeModule(negated, tokens, ref index, ref openModule, opaque, clauses);
            case "fragment":
            case "4":
            case "6":
                FlushOpaque(clauses, opaque, ref openModule);
                clauses.Add(OpaqueClause((negated ? "! " : string.Empty) + tokens[optionIndex].Format()));
                return true;
            default:
                if (TryConsumeStructured(option, negated, tokens, family, ref index, out FilterClause? clause))
                {
                    FlushOpaque(clauses, opaque, ref openModule);
                    if (clause is not null)
                    {
                        clauses.Add(clause);
                    }

                    return true;
                }

                index = optionIndex;
                if (negated)
                {
                    AppendOpaqueToken(opaque, ref openModule, "!");
                }

                AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
                index++;
                if (index < tokens.Count && tokens[index].Kind is IptablesTokenKind.Negation)
                {
                    AppendOpaqueToken(opaque, ref openModule, "!");
                    index++;
                }

                if (index < tokens.Count && tokens[index].Kind is IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString)
                {
                    AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
                    index++;
                }

                return true;
        }
    }

    private static bool TryConsumeTarget(
        string option,
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        ref int index,
        ref string? openModule,
        List<string> opaque,
        List<FilterClause> clauses,
        ref FilterTargetKind targetKind,
        ref string? targetName,
        ref List<string>? targetArguments)
    {
        if (negated || targetName is not null || index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            return false;
        }

        FlushOpaque(clauses, opaque, ref openModule);
        openModule = null;
        targetKind = option == "goto" ? FilterTargetKind.Goto : FilterTargetKind.Jump;
        targetName = tokens[index].Text;
        index++;
        targetArguments ??= [];
        while (index < tokens.Count && !IsClauseStarter(tokens[index]))
        {
            targetArguments.Add(tokens[index].Kind == IptablesTokenKind.Negation ? "!" : tokens[index].Format());
            index++;
        }

        return true;
    }

    private static bool TryConsumeModule(
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        ref int index,
        ref string? openModule,
        List<string> opaque,
        List<FilterClause> clauses)
    {
        if (index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            return false;
        }

        FlushOpaque(clauses, opaque, ref openModule);
        string module = tokens[index].Text;
        index++;
        if (!negated && s_transparentModules.Contains(module))
        {
            openModule = module;
            return true;
        }

        if (negated)
        {
            opaque.Add("!");
        }

        opaque.Add("-m");
        opaque.Add(module);
        openModule = module;
        return true;
    }

    private static bool TryConsumeStructured(
        string option,
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        ref int index,
        out FilterClause? clause)
    {
        clause = null;
        int start = index;
        if (option is "protocol" or "source" or "destination" or "in-interface" or "out-interface" or "source-port" or "destination-port" or "ports")
        {
            if (!TryReadValue(tokens, ref index, ref negated, out string value))
            {
                index = start;
                return false;
            }

            switch (option)
            {
                case "protocol":
                    if (!TryCanonicalProtocol(value, out string? protocol))
                    {
                        index = start;
                        return false;
                    }

                    if (protocol is null)
                    {
                        if (negated)
                        {
                            clause = Clause(FilterClauseKind.Protocol, negated: true, "all");
                        }

                        return true;
                    }

                    clause = Clause(FilterClauseKind.Protocol, negated, protocol);
                    return true;
                case "source":
                case "destination":
                    if (!TryCanonicalAddress(value, family, out string address))
                    {
                        index = start;
                        return false;
                    }

                    if (!negated && address == RuleSpecificationNormalizer.ANY)
                    {
                        return true;
                    }

                    clause = Clause(option == "source" ? FilterClauseKind.SourceAddress : FilterClauseKind.DestinationAddress, negated, address);
                    return true;
                case "in-interface":
                case "out-interface":
                    if (!IsInterfaceName(value))
                    {
                        index = start;
                        return false;
                    }

                    clause = Clause(option == "in-interface" ? FilterClauseKind.IngressInterface : FilterClauseKind.EgressInterface, negated, value);
                    return true;
                default:
                    if (!TryCanonicalPorts(value, out string ports))
                    {
                        index = start;
                        return false;
                    }

                    FilterClauseKind kind = option switch
                    {
                        "source-port" => FilterClauseKind.SourcePort,
                        "ports" => FilterClauseKind.EitherPort,
                        _ => FilterClauseKind.DestinationPort,
                    };
                    clause = Clause(kind, negated, ports);
                    return true;
            }
        }

        return false;
    }

    private static bool TryReadValue(IReadOnlyList<IptablesToken> tokens, ref int index, ref bool negated, out string value)
    {
        if (index < tokens.Count && tokens[index].Kind == IptablesTokenKind.Negation)
        {
            if (negated)
            {
                value = string.Empty;
                return false;
            }

            negated = true;
            index++;
        }

        if (index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            value = string.Empty;
            return false;
        }

        value = tokens[index].Text;
        index++;
        return true;
    }

    private static void AppendOpaqueToken(List<string> opaque, ref string? openModule, string text)
    {
        if (opaque.Count == 0 && openModule is not null)
        {
            opaque.Add("-m");
            opaque.Add(openModule);
        }

        opaque.Add(text);
    }

    private static void FlushOpaque(List<FilterClause> clauses, List<string> opaque, ref string? openModule)
    {
        if (opaque.Count == 0)
        {
            return;
        }

        clauses.Add(OpaqueClause(string.Join(' ', opaque)));
        opaque.Clear();
        openModule = null;
    }

    private static bool IsClauseStarter(IptablesToken token)
    {
        if (token.Kind == IptablesTokenKind.Negation)
        {
            return true;
        }

        if (!IsOption(token))
        {
            return false;
        }

        string option = CanonicalOption(token);
        return option is "protocol" or "source" or "destination" or "in-interface" or "out-interface"
            or "source-port" or "destination-port" or "ports" or "match" or "jump" or "goto"
            or "fragment" or "4" or "6";
    }

    private static bool IsOption(IptablesToken token) => token.Kind is IptablesTokenKind.ShortOption or IptablesTokenKind.LongOption;

    private static string CanonicalOption(IptablesToken token)
    {
        string name = token.Text.StartsWith("--", StringComparison.Ordinal) ? token.Text[2..] : token.Text[1..];
        return name switch
        {
            "p" or "protocol" => "protocol",
            "s" or "src" or "source" => "source",
            "d" or "dst" or "destination" => "destination",
            "i" or "in-interface" => "in-interface",
            "o" or "out-interface" => "out-interface",
            "m" or "match" => "match",
            "j" or "jump" => "jump",
            "g" or "goto" => "goto",
            "f" or "fragment" => "fragment",
            "4" => "4",
            "6" => "6",
            "sport" or "source-port" or "sports" or "source-ports" => "source-port",
            "dport" or "destination-port" or "dports" or "destination-ports" => "destination-port",
            "ports" => "ports",
            _ => name,
        };
    }

    private static bool TryCanonicalProtocol(string text, out string? protocol)
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

    private static bool TryCanonicalAddress(string text, FirewallAddressFamily family, out string canonical)
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

    private static bool TryCanonicalPorts(string text, out string ports)
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

    private static bool IsInterfaceName(string name)
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

    private static FilterClause Clause(FilterClauseKind kind, bool negated, string value) => new()
    {
        Kind = kind,
        Negated = negated,
        Value = value,
    };

    private static FilterClause OpaqueClause(string value) => Clause(FilterClauseKind.Opaque, negated: false, value);

    private static FilterTarget UnavailableTarget() => new()
    {
        Kind = FilterTargetKind.None,
        IsChainReference = false,
    };
}
